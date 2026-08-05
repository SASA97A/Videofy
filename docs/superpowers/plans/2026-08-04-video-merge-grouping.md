# Smart Video Merge with Dynamic Grouping Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement a smart video merging tab with dynamic group creation, context-menu group assignments, sequence ordering, visual DataGrid badges, a dedicated Group Manager window, and FFmpeg smart merge engine (lossless concat vs dynamic canvas re-encode).

**Architecture:** Extend `VideoFile` model with `GroupNumber` and `SequenceNumber`. Extend `FfprobeService` and `FfmpegService` with probing, compatibility checking, chapter generation, and concat filtergraph rendering. Add `MergeView.axaml` and `MergeGroupsWindow.axaml`. Wire up `MainWindowViewModel` to execute multi-group batch merges.

**Tech Stack:** C# .NET 9, Avalonia UI, CommunityToolkit.MVVM, System.Text.Json, FFmpeg CLI.

## Global Constraints
- Base Branch: `Version-1.4.3`
- Feature Branch: `feature/smart-video-merge`
- Min Group Size: At least 2 videos per group to merge.

---

### Task 1: Update VideoFile Data Model & Ffprobe Metadata Probing

**Files:**
- Modify: `Video Size Optimizer/Models/VideoFile.cs:20-60`
- Modify: `Video Size Optimizer/Services/FfprobeService.cs:140-159`

**Interfaces:**
- Consumes: `VideoFile` properties
- Produces: `VideoFile.GroupNumber`, `VideoFile.SequenceNumber`, `VideoFile.GroupOrderDisplay`, `VideoMetadata`, `FfprobeService.GetVideoMetadataAsync(filePath)`

- [ ] **Step 1: Update `VideoFile.cs` model**

Add observable properties:
```csharp
[ObservableProperty]
[NotifyPropertyChangedFor(nameof(HasGroup))]
[NotifyPropertyChangedFor(nameof(GroupOrderDisplay))]
private int? _groupNumber = null;

[ObservableProperty]
[NotifyPropertyChangedFor(nameof(GroupOrderDisplay))]
private int _sequenceNumber = 1;

public bool HasGroup => GroupNumber.HasValue;
public string GroupOrderDisplay => HasGroup ? $"G{GroupNumber} - #{SequenceNumber}" : string.Empty;
```

- [ ] **Step 2: Add metadata classes and probe method in `FfprobeService.cs`**

Define metadata classes:
```csharp
public class VideoStreamMetadata
{
    public string Codec { get; set; } = "";
    public int Width { get; set; }
    public int Height { get; set; }
    public double Fps { get; set; }
    public string PixFmt { get; set; } = "";
}

public class AudioStreamMetadata
{
    public bool Exists { get; set; }
    public string Codec { get; set; } = "";
    public int SampleRate { get; set; } = 48000;
    public int Channels { get; set; } = 2;
}

public class VideoMetadata
{
    public string Path { get; set; } = "";
    public double Duration { get; set; }
    public VideoStreamMetadata Video { get; set; } = new();
    public AudioStreamMetadata Audio { get; set; } = new();
}
```

Add `GetVideoMetadataAsync(string inputPath)` to `FfprobeService`:
```csharp
public async Task<VideoMetadata> GetVideoMetadataAsync(string inputPath)
{
    var meta = new VideoMetadata { Path = inputPath };
    if (!File.Exists(_ffprobePath)) return meta;

    var args = $"-v quiet -print_format json -show_format -show_streams \"{inputPath}\"";
    var startInfo = new ProcessStartInfo
    {
        FileName = _ffprobePath,
        Arguments = args,
        UseShellExecute = false,
        CreateNoWindow = true,
        RedirectStandardOutput = true
    };

    try
    {
        using var process = Process.Start(startInfo);
        if (process == null) return meta;

        string jsonOutput = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        using var doc = System.Text.Json.JsonDocument.Parse(jsonOutput);
        var root = doc.RootElement;

        if (root.TryGetProperty("format", out var formatEl))
        {
            if (formatEl.TryGetProperty("duration", out var durEl) &&
                double.TryParse(durEl.GetString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double duration))
            {
                meta.Duration = duration;
            }
        }

        if (root.TryGetProperty("streams", out var streamsEl))
        {
            foreach (var stream in streamsEl.EnumerateArray())
            {
                string codecType = stream.TryGetProperty("codec_type", out var ct) ? ct.GetString() ?? "" : "";
                if (codecType == "video" && string.IsNullOrEmpty(meta.Video.Codec))
                {
                    meta.Video.Codec = stream.TryGetProperty("codec_name", out var cn) ? cn.GetString() ?? "" : "";
                    meta.Video.Width = stream.TryGetProperty("width", out var w) ? w.GetInt32() : 0;
                    meta.Video.Height = stream.TryGetProperty("height", out var h) ? h.GetInt32() : 0;
                    meta.Video.PixFmt = stream.TryGetProperty("pix_fmt", out var pf) ? pf.GetString() ?? "" : "";

                    string fpsStr = stream.TryGetProperty("r_frame_rate", out var rfr) ? rfr.GetString() ?? "30/1" : "30/1";
                    var parts = fpsStr.Split('/');
                    if (parts.Length == 2 && double.TryParse(parts[0], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double num) &&
                        double.TryParse(parts[1], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double den) && den > 0)
                    {
                        meta.Video.Fps = num / den;
                    }
                    else
                    {
                        meta.Video.Fps = 30.0;
                    }
                }
                else if (codecType == "audio" && !meta.Audio.Exists)
                {
                    meta.Audio.Exists = true;
                    meta.Audio.Codec = stream.TryGetProperty("codec_name", out var acn) ? acn.GetString() ?? "" : "";
                    if (stream.TryGetProperty("sample_rate", out var sr) && int.TryParse(sr.GetString(), out int srVal))
                        meta.Audio.SampleRate = srVal;
                    if (stream.TryGetProperty("channels", out var ch))
                        meta.Audio.Channels = ch.GetInt32();
                }
            }
        }
    }
    catch (Exception ex)
    {
        LogService.Instance.Log($"Failed to probe video metadata for {inputPath}: {ex.Message}", LogLevel.Error, "FFPROBE");
    }

    return meta;
}
```

- [ ] **Step 3: Verify build**

Run: `dotnet build "Video Size Optimizer/Video Size Optimizer.csproj"`
Expected: Build succeeded with 0 Errors.

- [ ] **Step 4: Commit**

```bash
git add "Video Size Optimizer/Models/VideoFile.cs" "Video Size Optimizer/Services/FfprobeService.cs"
git commit -m "feat(model): add grouping properties to VideoFile and metadata probing to FfprobeService"
```

---

### Task 2: Implement FFmpeg Concat & Dynamic Canvas Merging Engine

**Files:**
- Modify: `Video Size Optimizer/Services/FfmpegService.cs:375-420`

**Interfaces:**
- Consumes: `VideoMetadata`, `FfmpegService.TestEncoderAsync`
- Produces: `FfmpegService.CheckMergeCompatibility`, `FfmpegService.GenerateChapterFile`, `FfmpegService.MergeVideosAsync`

- [ ] **Step 1: Add merge helper and execution methods to `FfmpegService.cs`**

```csharp
public bool CheckMergeCompatibility(List<VideoMetadata> metadataList)
{
    if (metadataList == null || metadataList.Count < 2) return false;
    var first = metadataList[0];
    for (int i = 1; i < metadataList.Count; i++)
    {
        var v1 = first.Video;
        var v2 = metadataList[i].Video;
        var a1 = first.Audio;
        var a2 = metadataList[i].Audio;

        if (v1.Codec != v2.Codec || v1.Width != v2.Width || v1.Height != v2.Height ||
            Math.Abs(v1.Fps - v2.Fps) > 0.05 || v1.PixFmt != v2.PixFmt)
            return false;

        if (a1.Exists != a2.Exists || a1.SampleRate != a2.SampleRate || a1.Channels != a2.Channels)
            return false;
    }
    return true;
}

public string GenerateChapterFile(List<VideoMetadata> metadataList)
{
    string metaFile = Path.Combine(Path.GetTempPath(), $"ffmetadata_{Guid.NewGuid()}.txt");
    using var writer = new StreamWriter(metaFile, false, System.Text.Encoding.UTF8);
    writer.WriteLine(";FFMETADATA1");

    double currentTime = 0.0;
    for (int i = 0; i < metadataList.Count; i++)
    {
        long startMs = (long)(currentTime * 1000);
        long durationMs = (long)(metadataList[i].Duration * 1000);
        long endMs = startMs + durationMs;
        string title = Path.GetFileNameWithoutExtension(metadataList[i].Path);

        writer.WriteLine("[CHAPTER]");
        writer.WriteLine("TIMEBASE=1/1000");
        writer.WriteLine($"START={startMs}");
        writer.WriteLine($"END={endMs}");
        writer.WriteLine($"title=Part {i + 1}: {title}");
        writer.WriteLine();

        currentTime += metadataList[i].Duration;
    }

    return metaFile;
}

public async Task MergeVideosAsync(List<VideoMetadata> metadataList, string outputPath, bool forceReencode, string encoder, IProgress<ConversionProgress>? progress = null)
{
    if (metadataList == null || metadataList.Count < 2) return;

    bool isCompatible = CheckMergeCompatibility(metadataList);
    string chapterFile = GenerateChapterFile(metadataList);

    try
    {
        if (isCompatible && !forceReencode)
        {
            LogService.Instance.Log("Streams are compatible. Using Lossless Concat Demuxer (-c copy)...", LogLevel.Info, "MERGE");
            string listFile = Path.Combine(Path.GetTempPath(), $"concat_{Guid.NewGuid()}.txt");
            using (var writer = new StreamWriter(listFile, false, System.Text.Encoding.UTF8))
            {
                foreach (var meta in metadataList)
                {
                    string safePath = meta.Path.Replace("'", "'\\''");
                    writer.WriteLine($"file '{safePath}'");
                }
            }

            var copyArgs = $"-y -f concat -safe 0 -i \"{listFile}\" -i \"{chapterFile}\" -map_metadata 1 -c copy \"{outputPath}\"";
            try
            {
                await RunFfmpegProcessAsync(copyArgs, progress);
            }
            finally
            {
                if (File.Exists(listFile)) File.Delete(listFile);
            }
        }
        else
        {
            LogService.Instance.Log("Re-encoding required. Building dynamic canvas filtergraph...", LogLevel.Info, "MERGE");
            int maxW = metadataList.Max(m => m.Video.Width);
            int maxH = metadataList.Max(m => m.Video.Height);
            if (maxW % 2 != 0) maxW++;
            if (maxH % 2 != 0) maxH++;
            double maxFps = metadataList.Max(m => m.Video.Fps);

            var filterChains = new List<string>();
            var inputArgs = new List<string>();

            for (int i = 0; i < metadataList.Count; i++)
            {
                var meta = metadataList[i];
                inputArgs.Add($"-i \"{meta.Path}\"");

                string vFilter = $"[{i}:v]scale=w='if(gt(iw/ih,{maxW}/{maxH}),{maxW},-2)':h='if(gt(iw/ih,{maxW}/{maxH}),-2,{maxH})':force_original_aspect_ratio=decrease," +
                                 $"pad=w={maxW}:h={maxH}:x='({maxW}-iw)/2':y='({maxH}-ih)/2':color=black," +
                                 $"fps={maxFps:F2},setsar=1[v{i}];";

                string aFilter = meta.Audio.Exists
                    ? $"[{i}:a]aformat=sample_fmts=fltp:sample_rates=48000:channel_layouts=stereo[a{i}];"
                    : $"anullsrc=channel_layout=stereo:sample_rate=48000,trim=duration={meta.Duration:F2}[a{i}];";

                filterChains.Add(vFilter + aFilter);
            }

            string concatInputs = string.Join("", Enumerable.Range(0, metadataList.Count).Select(i => $"[v{i}][a{i}]"));
            string concatFilter = $"{concatInputs}concat=n={metadataList.Count}:v=1:a=1[vout][aout]";
            string fullFiltergraph = string.Join("", filterChains) + concatFilter;

            inputArgs.Add($"-i \"{chapterFile}\"");

            string codecArgs;
            if (encoder.Contains("nvenc"))
                codecArgs = $"-c:v {encoder} -preset p5 -rc vbr -cq 23";
            else if (encoder.Contains("amf"))
                codecArgs = $"-c:v {encoder} -rc vbr_peak -qp_i 22 -qp_p 22";
            else if (encoder.Contains("qsv"))
                codecArgs = $"-c:v {encoder} -preset veryfast -global_quality 23";
            else
                codecArgs = "-c:v libx264 -crf 18";

            var args = $"-y {string.Join(" ", inputArgs)} -filter_complex \"{fullFiltergraph}\" -map \"[vout]\" -map \"[aout]\" -map_metadata {metadataList.Count} {codecArgs} -c:a aac -b:a 192k \"{outputPath}\"";
            await RunFfmpegProcessAsync(args, progress);
        }
    }
    finally
    {
        if (File.Exists(chapterFile)) File.Delete(chapterFile);
    }
}
```

- [ ] **Step 2: Verify build**

Run: `dotnet build "Video Size Optimizer/Video Size Optimizer.csproj"`
Expected: Build succeeded with 0 Errors.

- [ ] **Step 3: Commit**

```bash
git add "Video Size Optimizer/Services/FfmpegService.cs"
git commit -m "feat(ffmpeg): add smart merge execution methods and dynamic canvas filtergraph builder"
```

---

### Task 3: Create MergeView & Group Manager Window UI

**Files:**
- Create: `Video Size Optimizer/Views/MergeView.axaml`
- Create: `Video Size Optimizer/Views/MergeView.axaml.cs`
- Create: `Video Size Optimizer/Views/MergeGroupsWindow.axaml`
- Create: `Video Size Optimizer/Views/MergeGroupsWindow.axaml.cs`

**Interfaces:**
- Consumes: `MainWindowViewModel`
- Produces: `MergeView` UserControl, `MergeGroupsWindow` Window

- [ ] **Step 1: Create `MergeView.axaml` & `MergeView.axaml.cs`**

`MergeView.axaml`:
```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
             xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
             xmlns:vm="using:Video_Size_Optimizer.ViewModels"
             mc:Ignorable="d" d:DesignWidth="800" d:DesignHeight="450"
             x:Class="Video_Size_Optimizer.Views.MergeView"
             x:DataType="vm:MainWindowViewModel">

	<Border Background="{DynamicResource MainBackground}" Padding="15,12">
		<Grid ColumnDefinitions="*, Auto">
			<!-- CARD 1: Merge Settings (Left) -->
			<Border Grid.Column="0"
					Background="{DynamicResource SecondaryBackground}"
					BorderBrush="{DynamicResource BorderColor}"
					BorderThickness="1"
					CornerRadius="6"
					Padding="15,12"
					Margin="0,0,15,0"
					VerticalAlignment="Stretch">
				<StackPanel Spacing="8" VerticalAlignment="Center">
					<StackPanel Orientation="Horizontal" Spacing="8">
						<TextBlock Text="MERGE MODE" FontWeight="Black" FontSize="11" LetterSpacing="0.5" Foreground="{DynamicResource SystemAccentColor}"/>
						<TextBlock Text="(Smart Batch Segment Concatenation)" FontSize="12" Foreground="{DynamicResource SecondaryText}"/>
					</StackPanel>

					<StackPanel Orientation="Horizontal" Spacing="15" VerticalAlignment="Center">
						<StackPanel Orientation="Horizontal" Spacing="8">
							<TextBlock Text="Target Format:" VerticalAlignment="Center" FontSize="12" Foreground="{DynamicResource SecondaryText}"/>
							<ComboBox ItemsSource="{Binding OutputFormats}"
									  SelectedItem="{Binding MergeTargetFormat}"
									  IsEnabled="{Binding !IsBusy}"
									  Background="{DynamicResource InputBackground}"
									  BorderBrush="{DynamicResource BorderColor}"
									  Width="120" FontSize="12" FontWeight="Bold"
									  Cursor="Hand"/>
						</StackPanel>

						<CheckBox IsChecked="{Binding MergeForceReencode}"
								  Content="Force Re-encode"
								  IsEnabled="{Binding !IsBusy}"
								  FontSize="12"
								  Foreground="{DynamicResource MainText}"
								  Cursor="Hand"/>
					</StackPanel>

					<!-- Merge Info Banner -->
					<Border Background="#122438" BorderBrush="#1E4976" BorderThickness="1" CornerRadius="4" Padding="10,6" Margin="0,4,0,0">
						<StackPanel Spacing="2">
							<TextBlock Text="ℹ Smart Group Merging" Foreground="{DynamicResource InfoColor}" FontSize="11" FontWeight="Bold"/>
							<TextBlock Text="Right-click videos to add them to a merge group. Groups with 2+ videos will be merged into a single video (lossless copy when identical, smart dynamic canvas padding when mixed)."
									   Foreground="{DynamicResource SecondaryText}" FontSize="11" TextWrapping="Wrap"/>
						</StackPanel>
					</Border>
				</StackPanel>
			</Border>

			<!-- CARD 2: Actions (Right) -->
			<Border Grid.Column="1"
					Background="{DynamicResource SecondaryBackground}"
					BorderBrush="{DynamicResource BorderColor}"
					BorderThickness="1"
					CornerRadius="6"
					Padding="15,10"
					VerticalAlignment="Stretch">
				<StackPanel Orientation="Horizontal" Spacing="10" VerticalAlignment="Center">
					<Button Command="{Binding TogglePauseCommand}" IsVisible="{Binding IsBusy}" Width="42" Height="42" Background="{DynamicResource InputBackground}" BorderBrush="{DynamicResource BorderColor}" BorderThickness="1" CornerRadius="6" Cursor="Hand">
						<Panel>
							<Image Width="16" Height="16" IsVisible="{Binding !IsPaused}">
								<Image.Source>
									<SvgImage Source="avares://Videofy/Assets/pause.svg"/>
								</Image.Source>
							</Image>
							<Image Width="16" Height="16" IsVisible="{Binding IsPaused}">
								<Image.Source>
									<SvgImage Source="avares://Videofy/Assets/play.svg"/>
								</Image.Source>
							</Image>
						</Panel>
					</Button>
					<Button Command="{Binding HandleActionCommand}" Content="{Binding ActionButtonText}"
							Padding="24,0" Height="42" VerticalContentAlignment="Center" Cursor="Hand"
							Background="{Binding ActionButtonColor}" Foreground="{DynamicResource TertiaryBackground}"
							FontWeight="Black" FontSize="13" CornerRadius="6"/>
				</StackPanel>
			</Border>
		</Grid>
	</Border>
</UserControl>
```

`MergeView.axaml.cs`:
```csharp
using Avalonia.Controls;

namespace Video_Size_Optimizer.Views;

public partial class MergeView : UserControl
{
    public MergeView()
    {
        InitializeComponent();
    }
}
```

- [ ] **Step 2: Create `MergeGroupsWindow.axaml` & `MergeGroupsWindow.axaml.cs`**

`MergeGroupsWindow.axaml`:
```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:vm="using:Video_Size_Optimizer.ViewModels"
        xmlns:model="using:Video_Size_Optimizer.Models"
        x:Class="Video_Size_Optimizer.Views.MergeGroupsWindow"
        x:DataType="vm:MainWindowViewModel"
        Title="Merge Group Manager" Width="700" Height="450"
        WindowStartupLocation="CenterOwner"
        Background="{DynamicResource MainBackground}" Foreground="{DynamicResource MainText}">

	<Grid RowDefinitions="*, Auto" Margin="15">
		<Grid Grid.Row="0" ColumnDefinitions="200, *">
			<!-- Groups Left Panel -->
			<Border Grid.Column="0" Background="{DynamicResource SecondaryBackground}" BorderBrush="{DynamicResource BorderColor}" BorderThickness="1" CornerRadius="6" Margin="0,0,10,0" Padding="10">
				<DockPanel LastChildFill="True">
					<TextBlock DockPanel.Dock="Top" Text="Groups" FontWeight="Bold" FontSize="14" Foreground="{DynamicResource SystemAccentColor}" Margin="0,0,0,10"/>
					<ListBox ItemsSource="{Binding ExistingGroups}" SelectedItem="{Binding SelectedGroupOption}" Background="Transparent">
						<ListBox.ItemTemplate>
							<DataTemplate x:DataType="vm:GroupOption">
								<TextBlock Text="{Binding Name}" FontSize="13" FontWeight="SemiBold" Padding="6,4"/>
							</DataTemplate>
						</ListBox.ItemTemplate>
					</ListBox>
				</DockPanel>
			</Border>

			<!-- Group Contents Right Panel -->
			<Border Grid.Column="1" Background="{DynamicResource SecondaryBackground}" BorderBrush="{DynamicResource BorderColor}" BorderThickness="1" CornerRadius="6" Padding="10">
				<DockPanel LastChildFill="True">
					<TextBlock DockPanel.Dock="Top" Text="{Binding SelectedGroupTitle}" FontWeight="Bold" FontSize="14" Foreground="{DynamicResource SystemAccentColor}" Margin="0,0,0,10"/>
					
					<ItemsControl ItemsSource="{Binding SelectedGroupVideos}">
						<ItemsControl.ItemTemplate>
							<DataTemplate x:DataType="model:VideoFile">
								<Border Background="{DynamicResource InputBackground}" BorderBrush="{DynamicResource BorderColor}" BorderThickness="1" CornerRadius="4" Padding="10,6" Margin="0,3">
									<Grid ColumnDefinitions="Auto, *, Auto">
										<Border Grid.Column="0" Background="{DynamicResource SystemAccentColor}" CornerRadius="3" Padding="6,2" Margin="0,0,10,0" VerticalAlignment="Center">
											<TextBlock Text="{Binding SequenceNumber, StringFormat='#{0}'}" Foreground="Black" FontWeight="Bold" FontSize="11"/>
										</Border>
										
										<TextBlock Grid.Column="1" Text="{Binding FileName}" VerticalAlignment="Center" FontSize="12" Foreground="{DynamicResource MainText}"/>
										
										<StackPanel Grid.Column="2" Orientation="Horizontal" Spacing="4" VerticalAlignment="Center">
											<Button Content="▲" Command="{Binding $parent[Window].DataContext.MoveGroupVideoUpCommand}" CommandParameter="{Binding}" Padding="8,2" FontSize="10"/>
											<Button Content="▼" Command="{Binding $parent[Window].DataContext.MoveGroupVideoDownCommand}" CommandParameter="{Binding}" Padding="8,2" FontSize="10"/>
											<Button Content="❌" Command="{Binding $parent[Window].DataContext.RemoveGroupVideoCommand}" CommandParameter="{Binding}" Padding="8,2" FontSize="10"/>
										</StackPanel>
									</Grid>
								</Border>
							</DataTemplate>
						</ItemsControl.ItemTemplate>
					</ItemsControl>
				</DockPanel>
			</Border>
		</Grid>

		<Button Grid.Row="1" Content="Close" Click="OnCloseClick" HorizontalAlignment="Right" Margin="0,12,0,0" Background="{DynamicResource MainBackground}" Foreground="{DynamicResource MainText}" BorderBrush="{DynamicResource BorderColor}" Padding="20,6"/>
	</Grid>
</Window>
```

`MergeGroupsWindow.axaml.cs`:
```csharp
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Video_Size_Optimizer.Views;

public partial class MergeGroupsWindow : Window
{
    public MergeGroupsWindow()
    {
        InitializeComponent();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
```

- [ ] **Step 3: Verify build**

Run: `dotnet build "Video Size Optimizer/Video Size Optimizer.csproj"`
Expected: Build succeeded with 0 Errors.

- [ ] **Step 4: Commit**

```bash
git add "Video Size Optimizer/Views/MergeView.axaml" "Video Size Optimizer/Views/MergeView.axaml.cs" "Video Size Optimizer/Views/MergeGroupsWindow.axaml" "Video Size Optimizer/Views/MergeGroupsWindow.axaml.cs"
git commit -m "feat(ui): add MergeView and MergeGroupsWindow views for smart video merging"
```

---

### Task 4: Integrate Merge Tab, DataGrid Badge, and ViewModel Logic

**Files:**
- Modify: `Video Size Optimizer/ViewModels/MainWindowViewModel.cs:30-100`
- Modify: `Video Size Optimizer/Views/MainWindow.axaml:350-400,620-630`

**Interfaces:**
- Consumes: `VideoFile.GroupNumber`, `FfmpegService.MergeVideosAsync`
- Produces: `MainWindowViewModel.MergeTargetFormat`, `MainWindowViewModel.MergeForceReencode`, `MainWindowViewModel.ExistingGroups`, `MainWindowViewModel.SelectedGroupOption`, `MainWindowViewModel.SelectedGroupVideos`, batch merging execution logic.

- [ ] **Step 1: Update `MainWindowViewModel.cs`**

Add properties, group management commands, and batch merge execution:
```csharp
[ObservableProperty] private string _mergeTargetFormat = ".mp4";
[ObservableProperty] private bool _mergeForceReencode = false;

public bool IsMergeTabActive => SelectedTabIndex == 3;
public ObservableCollection<GroupOption> ExistingGroups { get; } = new();

[ObservableProperty]
[NotifyPropertyChangedFor(nameof(SelectedGroupTitle))]
[NotifyPropertyChangedFor(nameof(SelectedGroupVideos))]
private GroupOption? _selectedGroupOption;

public string SelectedGroupTitle => SelectedGroupOption != null ? $"Items in {SelectedGroupOption.Name}" : "Select a group to inspect";

public IEnumerable<VideoFile> SelectedGroupVideos
{
    get
    {
        if (SelectedGroupOption == null) return Enumerable.Empty<VideoFile>();
        return Videos.Where(v => v.GroupNumber == SelectedGroupOption.Id).OrderBy(v => v.SequenceNumber);
    }
}

public void RefreshExistingGroups()
{
    var currentGroupIds = Videos.Where(v => v.HasGroup).Select(v => v.GroupNumber!.Value).Distinct().OrderBy(g => g).ToList();
    
    // Sync ExistingGroups collection
    var toRemove = ExistingGroups.Where(g => !currentGroupIds.Contains(g.Id)).ToList();
    foreach (var r in toRemove) ExistingGroups.Remove(r);

    foreach (var id in currentGroupIds)
    {
        if (!ExistingGroups.Any(g => g.Id == id))
        {
            ExistingGroups.Add(new GroupOption { Id = id });
        }
    }

    if (SelectedGroupOption == null || !currentGroupIds.Contains(SelectedGroupOption.Id))
    {
        SelectedGroupOption = ExistingGroups.FirstOrDefault();
    }

    OnPropertyChanged(nameof(SelectedGroupVideos));
}

[RelayCommand]
public void AddToNewGroup()
{
    var selected = Videos.Where(v => v.IsSelected).ToList();
    if (selected.Count == 0) return;

    int newId = ExistingGroups.Count > 0 ? ExistingGroups.Max(g => g.Id) + 1 : 1;
    int seq = 1;
    foreach (var v in selected)
    {
        v.GroupNumber = newId;
        v.SequenceNumber = seq++;
    }
    RefreshExistingGroups();
}

[RelayCommand]
public void AssignToGroup(int groupId)
{
    var selected = Videos.Where(v => v.IsSelected).ToList();
    if (selected.Count == 0) return;

    int maxSeq = Videos.Where(v => v.GroupNumber == groupId).Select(v => v.SequenceNumber).DefaultIfEmpty(0).Max();
    foreach (var v in selected)
    {
        v.GroupNumber = groupId;
        v.SequenceNumber = ++maxSeq;
    }
    RefreshExistingGroups();
}

[RelayCommand]
public void RemoveFromGroup()
{
    var selected = Videos.Where(v => v.IsSelected).ToList();
    foreach (var v in selected)
    {
        int? oldGroup = v.GroupNumber;
        v.GroupNumber = null;
        if (oldGroup.HasValue) ReNormalizeGroupSequence(oldGroup.Value);
    }
    RefreshExistingGroups();
}

private void ReNormalizeGroupSequence(int groupId)
{
    int seq = 1;
    foreach (var v in Videos.Where(v => v.GroupNumber == groupId).OrderBy(v => v.SequenceNumber))
    {
        v.SequenceNumber = seq++;
    }
}

[RelayCommand]
public void MoveGroupVideoUp(VideoFile video)
{
    if (video == null || !video.HasGroup) return;
    var groupList = Videos.Where(v => v.GroupNumber == video.GroupNumber).OrderBy(v => v.SequenceNumber).ToList();
    int idx = groupList.IndexOf(video);
    if (idx > 0)
    {
        var prev = groupList[idx - 1];
        int tmp = video.SequenceNumber;
        video.SequenceNumber = prev.SequenceNumber;
        prev.SequenceNumber = tmp;
        ReNormalizeGroupSequence(video.GroupNumber!.Value);
        OnPropertyChanged(nameof(SelectedGroupVideos));
    }
}

[RelayCommand]
public void MoveGroupVideoDown(VideoFile video)
{
    if (video == null || !video.HasGroup) return;
    var groupList = Videos.Where(v => v.GroupNumber == video.GroupNumber).OrderBy(v => v.SequenceNumber).ToList();
    int idx = groupList.IndexOf(video);
    if (idx >= 0 && idx < groupList.Count - 1)
    {
        var next = groupList[idx + 1];
        int tmp = video.SequenceNumber;
        video.SequenceNumber = next.SequenceNumber;
        next.SequenceNumber = tmp;
        ReNormalizeGroupSequence(video.GroupNumber!.Value);
        OnPropertyChanged(nameof(SelectedGroupVideos));
    }
}

[RelayCommand]
public void RemoveGroupVideo(VideoFile video)
{
    if (video == null || !video.HasGroup) return;
    int oldGroup = video.GroupNumber!.Value;
    video.GroupNumber = null;
    ReNormalizeGroupSequence(oldGroup);
    RefreshExistingGroups();
}

[RelayCommand]
public async Task OpenGroupManager(Window owner)
{
    RefreshExistingGroups();
    var win = new Views.MergeGroupsWindow { DataContext = this };
    await win.ShowDialog(owner);
}
```

Update `SelectedTabIndex` setter to trigger `OnPropertyChanged(nameof(IsMergeTabActive))`.
In `StartCompressionAsync()` logic for `SelectedTabIndex == 3`:
```csharp
if (SelectedTabIndex == 3)
{
    var activeGroups = Videos.Where(v => v.HasGroup).GroupBy(v => v.GroupNumber!.Value).Where(g => g.Count() >= 2).ToList();
    if (activeGroups.Count == 0)
    {
        LogService.Instance.Log("No valid merge groups found (groups must contain at least 2 videos). Batch aborted.", LogLevel.Warning, "MERGE");
        await _messageService.ShowInfoAsync("No Merge Groups", "Please assign at least 2 videos to a merge group before starting.");
        return;
    }

    foreach (var group in activeGroups)
    {
        if (!IsBusy) break;

        int groupId = group.Key;
        var groupFiles = group.OrderBy(v => v.SequenceNumber).ToList();
        LogService.Instance.Section($"Merging Group {groupId} ({groupFiles.Count} videos)");

        var metaList = new List<VideoMetadata>();
        foreach (var v in groupFiles)
        {
            v.IsProcessing = true;
            metaList.Add(await _ffprobeService.GetVideoMetadataAsync(v.FilePath));
        }

        string firstFilePath = groupFiles[0].FilePath;
        string outputExt = MergeTargetFormat;
        string finalPath = _fileService.GenerateOutputPath(firstFilePath, 0, outputExt);
        finalPath = finalPath.Replace($"-CRF0", $"_merged_Group{groupId}");

        var p = new Progress<ConversionProgress>(cp =>
        {
            foreach (var v in groupFiles) v.UpdateProgress(cp.Percentage, cp.Speed, cp.Fps);
            CurrentSpeed = cp.Speed;
        });

        if (!AppConstants.EncoderMap.TryGetValue(SelectedEncoder, out string? encoderValue))
            encoderValue = "libx264";

        await _ffmpegService.MergeVideosAsync(metaList, finalPath, MergeForceReencode, encoderValue, p);

        foreach (var v in groupFiles)
        {
            v.IsCompleted = true;
            v.Progress = 100;
            v.IsProcessing = false;
        }

        LogService.Instance.Log($"Group {groupId} merged successfully -> {finalPath}", LogLevel.Success, "MERGE");
    }
    return;
}
```

- [ ] **Step 2: Update `MainWindow.axaml` ContextMenu & DataGrid Column**

Add Merge Group Column to DataGrid:
```xml
<DataGridTemplateColumn Header="Group" Width="100" CanUserResize="False">
    <DataGridTemplateColumn.CellTemplate>
        <DataTemplate>
            <Border IsVisible="{Binding HasGroup}"
                    Background="{DynamicResource SystemAccentColor}"
                    CornerRadius="4"
                    Padding="6,2"
                    HorizontalAlignment="Center"
                    VerticalAlignment="Center">
                <TextBlock Text="{Binding GroupOrderDisplay}"
                           Foreground="Black"
                           FontWeight="Bold"
                           FontSize="11"/>
            </Border>
        </DataTemplate>
    </DataGridTemplateColumn.CellTemplate>
</DataGridTemplateColumn>
```

Add ContextMenu items:
```xml
<MenuItem Header="Assign to Merge Group" IsVisible="{Binding $parent[DataGrid].DataContext.IsMergeTabActive}">
    <MenuItem Header="Add to New Group" Command="{Binding $parent[DataGrid].DataContext.AddToNewGroupCommand}"/>
    <MenuItem Header="Add to Existing Group" IsEnabled="{Binding $parent[DataGrid].DataContext.HasExistingGroups}" ItemsSource="{Binding $parent[DataGrid].DataContext.ExistingGroups}">
        <MenuItem.ItemTemplate>
            <DataTemplate x:DataType="vm:GroupOption">
                <MenuItem Header="{Binding Name}" Command="{Binding $parent[DataGrid].DataContext.AssignToGroupCommand}" CommandParameter="{Binding Id}"/>
            </DataTemplate>
        </MenuItem.ItemTemplate>
    </MenuItem>
    <Separator/>
    <MenuItem Header="Remove from Group" Command="{Binding $parent[DataGrid].DataContext.RemoveFromGroupCommand}"/>
</MenuItem>

<MenuItem Header="Show Group Manager..." IsVisible="{Binding $parent[DataGrid].DataContext.IsMergeTabActive}" Command="{Binding $parent[DataGrid].DataContext.OpenGroupManagerCommand}" CommandParameter="{Binding $parent[Window]}"/>
```

Add Tab 4 (`MERGE`):
```xml
<TabItem Header="MERGE (SMART BATCH)" IsEnabled="{Binding !IsBusy}" Cursor="Hand">
    <Views:MergeView/>
</TabItem>
```

- [ ] **Step 3: Verify build**

Run: `dotnet build "Video Size Optimizer/Video Size Optimizer.csproj"`
Expected: Build succeeded with 0 Errors.

- [ ] **Step 4: Commit**

```bash
git add "Video Size Optimizer/ViewModels/MainWindowViewModel.cs" "Video Size Optimizer/Views/MainWindow.axaml"
git commit -m "feat(merge): integrate batch video merge logic, context menu options, and UI bindings"
```

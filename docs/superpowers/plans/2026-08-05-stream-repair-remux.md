# Stream Repair & Remux Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement a new 5th tab ("REPAIR (STREAM FIX)") in Videofy for repairing corrupted transport streams (.ts, .mts, live recordings, damaged containers) with user-selectable GPU/CPU encoders, full multi-track audio & subtitle preservation via AAC/mov_text transcoding, and CFR faststart timing.

**Architecture:** Extend Avalonia UI tab controls with `RepairView`, extend `FileService` with `GenerateRepairOutputPath`, extend `FfmpegService` with `RepairVideoAsync` incorporating FFmpeg repair flags (`+genpts+discardcorrupt`, `ignore_err`, `200M` probe), and wire `MainWindowViewModel` to handle `SelectedTabIndex == 4`.

**Tech Stack:** C# 12, .NET 8, Avalonia UI 11, CommunityToolkit.MVVM, FFmpeg CLI.

## Global Constraints
- Target Framework: .NET 8.0 / Avalonia 11
- Build command: `dotnet build "Video Size Optimizer/Video Size Optimizer.csproj"`
- Code conventions: Standard Videofy MVVM architecture, CommunityToolkit.Mvvm source generators (`[ObservableProperty]`, `[RelayCommand]`), absolute output path resolution, non-blocking async processes.

---

### Task 1: Add Output Path Generator for Stream Repair in `FileService.cs`

**Files:**
- Modify: `Video Size Optimizer/Services/FileService.cs`

**Interfaces:**
- Produces: `public string GenerateRepairOutputPath(string inputPath, string extension)`

- [ ] **Step 1: Edit `FileService.cs` to add `GenerateRepairOutputPath`**

Add `GenerateRepairOutputPath` method in `FileService.cs`:
```csharp
public string GenerateRepairOutputPath(string inputPath, string extension)
{
    return BuildFinalPath(inputPath, "_repaired", ResolveOutputExtension(inputPath, extension));
}
```

- [ ] **Step 2: Build project to verify `FileService.cs` compiles**

Run: `dotnet build "Video Size Optimizer/Video Size Optimizer.csproj"`
Expected: Build succeeded with 0 errors.

- [ ] **Step 3: Commit Task 1**

```bash
git add "Video Size Optimizer/Services/FileService.cs"
git commit -m "feat: add GenerateRepairOutputPath to FileService"
```

---

### Task 2: Add `RepairVideoAsync` in `FfmpegService.cs`

**Files:**
- Modify: `Video Size Optimizer/Services/FfmpegService.cs`

**Interfaces:**
- Consumes: `FileService`, `RunFfmpegProcessAsync`
- Produces: `public async Task RepairVideoAsync(string input, string output, string encoder, string trimArgs, IProgress<ConversionProgress>? progress = null)`

- [ ] **Step 1: Edit `FfmpegService.cs` to implement `RepairVideoAsync`**

Add `RepairVideoAsync` method to `FfmpegService.cs`:
```csharp
public async Task RepairVideoAsync(string input, string output, string encoder, string trimArgs, IProgress<ConversionProgress>? progress = null)
{
    try
    {
        string vopts;
        if (encoder.Contains("nvenc"))
        {
            vopts = "-preset p5 -cq 20 -b:v 0";
        }
        else if (encoder.Contains("amf"))
        {
            vopts = "-rc vbr_peak -qp_i 20 -qp_p 20 -quality quality";
        }
        else if (encoder.Contains("qsv"))
        {
            vopts = "-preset veryfast -global_quality 20";
        }
        else
        {
            vopts = "-preset slow -crf 18";
        }

        string extension = Path.GetExtension(output).ToLowerInvariant();
        bool isMkv = extension == ".mkv";
        string subCodec = isMkv ? "-c:s copy" : "-c:s mov_text";

        string repairFlags = "-fflags +genpts+discardcorrupt -err_detect ignore_err -analyzeduration 200M -probesize 200M";
        string streamMaps = "-map 0:v:0 -map 0:a? -map 0:s?";
        string videoEncoderArgs = $"-c:v {encoder} {vopts} -pix_fmt yuv420p";
        string audioEncoderArgs = "-c:a aac -b:a 192k -ar 48000 -af aresample=async=1000";
        string timingFlags = "-fps_mode cfr -movflags +faststart -max_interleave_delta 0 -avoid_negative_ts make_zero";

        var args = $"-hide_banner -y {repairFlags} {trimArgs} -i \"{input}\" {streamMaps} {videoEncoderArgs} {audioEncoderArgs} {subCodec} {timingFlags} \"{output}\"";

        await RunFfmpegProcessAsync(args, progress);
    }
    catch (Exception ex)
    {
        LogService.Instance.Log($"Stream repair failed. Input={input} | Output={output} | Error: {ex.Message}", LogLevel.Error, "FFMPEG");
        throw;
    }
}
```

- [ ] **Step 2: Build project to verify `FfmpegService.cs` compiles**

Run: `dotnet build "Video Size Optimizer/Video Size Optimizer.csproj"`
Expected: Build succeeded with 0 errors.

- [ ] **Step 3: Commit Task 2**

```bash
git add "Video Size Optimizer/Services/FfmpegService.cs"
git commit -m "feat: add RepairVideoAsync to FfmpegService"
```

---

### Task 3: Create `RepairView.axaml` and `RepairView.axaml.cs`

**Files:**
- Create: `Video Size Optimizer/Views/RepairView.axaml`
- Create: `Video Size Optimizer/Views/RepairView.axaml.cs`

**Interfaces:**
- Consumes: `MainWindowViewModel` properties (`AvailableEncoders`, `RepairSelectedEncoder`, `OutputFormats`, `RepairTargetFormat`, `IsBusy`, `IsPaused`, `ActionButtonText`, `ActionButtonColor`, commands)

- [ ] **Step 1: Create `RepairView.axaml.cs`**

```csharp
using Avalonia.Controls;

namespace Video_Size_Optimizer.Views;

public partial class RepairView : UserControl
{
    public RepairView()
    {
        InitializeComponent();
    }
}
```

- [ ] **Step 2: Create `RepairView.axaml`**

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
             xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
			 xmlns:vm="using:Video_Size_Optimizer.ViewModels"
             mc:Ignorable="d" d:DesignWidth="800" d:DesignHeight="450"
             x:Class="Video_Size_Optimizer.Views.RepairView"
			 x:DataType="vm:MainWindowViewModel">

	<Border Background="{DynamicResource MainBackground}" Padding="15,12">
		<Grid ColumnDefinitions="*, Auto">
			<!-- CARD 1: Stream Repair Settings (Left) -->
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
						<TextBlock Text="STREAM REPAIR MODE" FontWeight="Black" FontSize="11" LetterSpacing="0.5" Foreground="{DynamicResource SystemAccentColor}"/>
						<TextBlock Text="(Fix Corrupted TS &amp; Live Recordings)" FontSize="12" Foreground="{DynamicResource SecondaryText}"/>
					</StackPanel>

					<StackPanel Orientation="Horizontal" Spacing="15" VerticalAlignment="Center">
						<StackPanel Orientation="Horizontal" Spacing="8">
							<TextBlock Text="Encoder:" VerticalAlignment="Center" FontSize="12" Foreground="{DynamicResource SecondaryText}"/>
							<ComboBox ItemsSource="{Binding AvailableEncoders}"
									  SelectedItem="{Binding RepairSelectedEncoder}"
									  IsEnabled="{Binding !IsBusy}"
									  Background="{DynamicResource InputBackground}"
									  BorderBrush="{DynamicResource BorderColor}"
									  Width="190" FontSize="12"
									  Cursor="Hand"
									  ToolTip.Tip="Select video encoder (GPU hardware acceleration or CPU standard encoder) used for stream reconstruction."/>
						</StackPanel>

						<StackPanel Orientation="Horizontal" Spacing="8">
							<TextBlock Text="Target Format:" VerticalAlignment="Center" FontSize="12" Foreground="{DynamicResource SecondaryText}"/>
							<ComboBox ItemsSource="{Binding OutputFormats}"
									  SelectedItem="{Binding RepairTargetFormat}"
									  IsEnabled="{Binding !IsBusy}"
									  Background="{DynamicResource InputBackground}"
									  BorderBrush="{DynamicResource BorderColor}"
									  Width="110" FontSize="12" FontWeight="Bold"
									  Cursor="Hand"
									  ToolTip.Tip="Select container format for repaired file (default: .mp4)."/>
						</StackPanel>
					</StackPanel>

					<!-- Stream Repair Info Banner -->
					<Border Background="#122438" BorderBrush="#1E4976" BorderThickness="1" CornerRadius="4" Padding="10,6" Margin="0,4,0,0">
						<StackPanel Spacing="2">
							<TextBlock Text="🛠 Stream Repair &amp; Remux Engine" Foreground="{DynamicResource InfoColor}" FontSize="11" FontWeight="Bold"/>
							<TextBlock Text="Fixes corrupted transport streams (.ts, .mts, live recordings) by discarding corrupt frames, generating missing PTS timestamps, transcoding all audio streams to 48kHz AAC, preserving subtitle tracks, and enforcing CFR timing for smooth playback."
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
					<Button Command="{Binding TogglePauseCommand}" IsVisible="{Binding IsBusy}" Width="42" Height="42" Background="{DynamicResource InputBackground}" BorderBrush="{DynamicResource BorderColor}" BorderThickness="1" CornerRadius="6" Cursor="Hand" ToolTip.Tip="Pause or Resume Processing">
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

- [ ] **Step 3: Build project to verify `RepairView` compiles**

Run: `dotnet build "Video Size Optimizer/Video Size Optimizer.csproj"`
Expected: Build succeeded with 0 errors.

- [ ] **Step 4: Commit Task 3**

```bash
git add "Video Size Optimizer/Views/RepairView.axaml" "Video Size Optimizer/Views/RepairView.axaml.cs"
git commit -m "feat: add RepairView control for stream repair tab"
```

---

### Task 4: Integrate Stream Repair in `MainWindowViewModel.cs` & `MainWindow.axaml`

**Files:**
- Modify: `Video Size Optimizer/ViewModels/MainWindowViewModel.cs`
- Modify: `Video Size Optimizer/Views/MainWindow.axaml`

**Interfaces:**
- Consumes: `FfmpegService.RepairVideoAsync`, `FileService.GenerateRepairOutputPath`, `RepairView`
- Produces: UI tab navigation, batch stream repair execution

- [ ] **Step 1: Edit `MainWindowViewModel.cs`**

1. Add properties:
```csharp
[ObservableProperty] private string _repairSelectedEncoder = "Standard (Slow, Best Quality)";
[ObservableProperty] private string _repairTargetFormat = ".mp4";
```
2. Update hardware detection completion fallback:
In constructor hardware detection block:
```csharp
if (!AvailableEncoders.Contains(RepairSelectedEncoder))
{
    RepairSelectedEncoder = AvailableEncoders.First();
}
```
3. Update `StartCompressionAsync`:
- Log mode description:
```csharp
LogService.Instance.Log($"Mode: {(SelectedTabIndex == 0 ? "Encode" : SelectedTabIndex == 1 ? "Stream Copy" : SelectedTabIndex == 3 ? "Merge" : SelectedTabIndex == 4 ? "Stream Repair" : "Split")}");
```
- In loop for `selectedVideos`, add `SelectedTabIndex == 4` case:
```csharp
else if (SelectedTabIndex == 4)
{
    finalOutputPath = _fileService.GenerateRepairOutputPath(video.FilePath, RepairTargetFormat);

    if (!AppConstants.EncoderMap.TryGetValue(RepairSelectedEncoder, out string? encoderValue))
        encoderValue = "libx264";

    StatusMessage = "Repairing Stream...";
    await _ffmpegService.RepairVideoAsync(video.FilePath, finalOutputPath, encoderValue, trimArgs, p);

    LogService.Instance.Log($"Stream repair completed for {video.FileName} -> {finalOutputPath}");
}
```
- Make sure original file cleanup ignores repair mode (tab 4):
```csharp
if (SelectedTabIndex == 0 && GlobalSettings.DeleteOriginalAfterCompression && File.Exists(finalOutputPath) && !video.IsSplitEnabled)
```

- [ ] **Step 2: Edit `MainWindow.axaml`**

In `TabControl`:
Add `TabItem` for Repair after Merge tab:
```xml
<TabItem Header="REPAIR (STREAM FIX)" IsEnabled="{Binding !IsBusy}" Cursor="Hand">
    <Views:RepairView/>
</TabItem>
```

- [ ] **Step 3: Build project to verify full integration**

Run: `dotnet build "Video Size Optimizer/Video Size Optimizer.csproj"`
Expected: Build succeeded with 0 errors.

- [ ] **Step 4: Commit Task 4**

```bash
git add "Video Size Optimizer/ViewModels/MainWindowViewModel.cs" "Video Size Optimizer/Views/MainWindow.axaml"
git commit -m "feat: integrate stream repair tab into MainWindow and ViewModel"
```

---

### Verification Plan
1. **Build Verification**: Run `dotnet build "Video Size Optimizer/Video Size Optimizer.csproj"` and ensure clean build.
2. **Runtime Code Inspection**: Verify `RepairVideoAsync` executes FFmpeg args cleanly and `GenerateRepairOutputPath` creates `_repaired` output filenames.

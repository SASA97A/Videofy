# GPU Hardware Auto-Detection Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement automatic cross-platform GPU hardware detection (NVENC, AMF, QSV) for Videofy v1.4.3, graying out unsupported hardware options in the Settings UI while auto-enabling working ones.

**Architecture:** Probe hardware via silent, lightweight FFmpeg dummy encodes. Store detection results in `AppSettings`, bind `SettingsWindow.axaml` checkbox `IsEnabled` to hardware support status, and auto-trigger hardware detection on initial application startup and via a manual "Auto-detect" button.

**Tech Stack:** C# .NET 9, Avalonia UI, CommunityToolkit.MVVM, FFmpeg CLI.

## Global Constraints
- **Version Floor:** Videofy v1.4.3
- **Platform Compatibility:** Windows, macOS, Linux (dynamic null device: `NUL` vs `/dev/null`)
- **Always Available:** CPU Encoder `Standard (Slow, Best Quality)` (`libx265`)

---

### Task 1: Update App Version & Data Models

**Files:**
- Modify: `Video Size Optimizer/Utils/AppConstants.cs:10`
- Modify: `Video Size Optimizer/Models/AppSettings.cs:7-19`

**Interfaces:**
- Consumes: `AppConstants.AppVersion`
- Produces: `AppSettings.SupportedHardwareEncoders`, `AppSettings.HasDetectedHardware`

- [ ] **Step 1: Update `AppConstants.cs` version string**

Change `AppVersion` from `"v1.4.2"` to `"v1.4.3"`.

- [ ] **Step 2: Update `AppSettings.cs` model**

Add `SupportedHardwareEncoders` list and `HasDetectedHardware` boolean flag:
```csharp
public List<string> SupportedHardwareEncoders { get; set; } = new();
public bool HasDetectedHardware { get; set; } = false;
```

- [ ] **Step 3: Verify build**

Run: `dotnet build "Video Size Optimizer/Video Size Optimizer.csproj"`
Expected: Build succeeded with 0 Errors.

- [ ] **Step 4: Commit**

```bash
git add "Video Size Optimizer/Utils/AppConstants.cs" "Video Size Optimizer/Models/AppSettings.cs"
git commit -m "feat(models): bump app version to v1.4.3 and update AppSettings model"
```

---

### Task 2: Implement FFmpeg Hardware Encoder Probing Methods

**Files:**
- Modify: `Video Size Optimizer/Services/FfmpegService.cs:375-376`

**Interfaces:**
- Consumes: `AppConstants.EncoderMap`, `AppPathService.FfmpegExecutable`
- Produces: `FfmpegService.TestEncoderAsync(string encoder)`, `FfmpegService.DetectSupportedHardwareEncodersAsync()`

- [ ] **Step 1: Implement `TestEncoderAsync` and `DetectSupportedHardwareEncodersAsync` in `FfmpegService.cs`**

Add the probing methods to `FfmpegService.cs`:
```csharp
public async Task<bool> TestEncoderAsync(string encoder)
{
    if (!File.Exists(_ffmpegPath)) return false;

    try
    {
        string nullDev = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "NUL" : "/dev/null";
        var args = $"-y -f lavfi -i color=c=black:s=256x256:d=0.1 -c:v {encoder} -f null {nullDev}";
        
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _ffmpegPath,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            }
        };

        process.Start();
        await process.WaitForExitAsync();
        return process.ExitCode == 0;
    }
    catch
    {
        return false;
    }
}

public async Task<List<string>> DetectSupportedHardwareEncodersAsync()
{
    var supported = new List<string>();
    foreach (var kvp in Video_Size_Optimizer.Utils.AppConstants.EncoderMap)
    {
        if (kvp.Key.Contains("Standard")) continue;
        if (await TestEncoderAsync(kvp.Value))
        {
            supported.Add(kvp.Key);
        }
    }
    return supported;
}
```

- [ ] **Step 2: Verify build**

Run: `dotnet build "Video Size Optimizer/Video Size Optimizer.csproj"`
Expected: Build succeeded with 0 Errors.

- [ ] **Step 3: Commit**

```bash
git add "Video Size Optimizer/Services/FfmpegService.cs"
git commit -m "feat(ffmpeg): add encoder probing methods for hardware detection"
```

---

### Task 3: Update Settings ViewModel & Settings UI Window

**Files:**
- Modify: `Video Size Optimizer/ViewModels/SettingsViewModel.cs:22-77`
- Modify: `Video Size Optimizer/Views/SettingsWindow.axaml:80-113`

**Interfaces:**
- Consumes: `FfmpegService.DetectSupportedHardwareEncodersAsync()`, `AppSettings.SupportedHardwareEncoders`
- Produces: `EncoderOption.IsSupported`, `SettingsViewModel.AutoDetectCommand`

- [ ] **Step 1: Update `EncoderOption` class in `SettingsViewModel.cs`**

Add `_isSupported` observable property:
```csharp
public partial class EncoderOption : ObservableObject
{
    public string Name { get; set; } = "";
    [ObservableProperty] private bool _isIncluded;
    [ObservableProperty] private bool _isSupported;
}
```

- [ ] **Step 2: Update `SettingsViewModel` constructor and add `AutoDetectCommand`**

```csharp
private readonly FfmpegService _ffmpegService = new();

public SettingsViewModel(Models.AppSettings currentSettings)
{
    DeleteOriginal = currentSettings.DeleteOriginalAfterCompression;
    SelectedFormat = currentSettings.DefaultOutputFormat;
    PreventSleep = currentSettings.PreventSleep;
    LowDiskBufferGb = currentSettings.LowDiskBufferGb;
    ProcessAlreadyOptimized = currentSettings.ProcessAlreadyOptimized;
    CustomExtensions = currentSettings.CustomExtensions;
    PreventUpsampling = currentSettings.PreventUpsampling;
    UseSoftwareRendering = currentSettings.UseSoftwareRendering;
    AutoCheckUpdates = currentSettings.AutoCheckUpdatesOnStartup;

    foreach (var name in AppConstants.HardwareEncoderNames)
    {
        bool isSupp = currentSettings.SupportedHardwareEncoders.Contains(name);
        EncoderOptions.Add(new EncoderOption
        {
            Name = name,
            IsIncluded = currentSettings.EnabledEncoders.Contains(name) && isSupp,
            IsSupported = isSupp
        });
    }
}

[RelayCommand]
public async Task AutoDetectHardware()
{
    var detected = await _ffmpegService.DetectSupportedHardwareEncodersAsync();
    foreach (var option in EncoderOptions)
    {
        bool isSupp = detected.Contains(option.Name);
        option.IsSupported = isSupp;
        if (isSupp)
        {
            option.IsIncluded = true;
        }
        else
        {
            option.IsIncluded = false;
        }
    }
}

public Models.AppSettings GetUpdatedSettings()
{
    var enabled = new List<string> { "Standard (Slow, Best Quality)" };
    enabled.AddRange(EncoderOptions.Where(x => x.IsIncluded && x.IsSupported).Select(x => x.Name));

    var supported = EncoderOptions.Where(x => x.IsSupported).Select(x => x.Name).ToList();

    return new Models.AppSettings
    {
        DeleteOriginalAfterCompression = DeleteOriginal,
        DefaultOutputFormat = SelectedFormat,
        PreventSleep = PreventSleep,
        LowDiskBufferGb = LowDiskBufferGb,
        ProcessAlreadyOptimized = ProcessAlreadyOptimized,
        EnabledEncoders = enabled,
        SupportedHardwareEncoders = supported,
        HasDetectedHardware = true,
        CustomExtensions = CustomExtensions,
        PreventUpsampling = PreventUpsampling,
        UseSoftwareRendering = UseSoftwareRendering,
        AutoCheckUpdatesOnStartup = AutoCheckUpdates
    };
}
```

- [ ] **Step 3: Update `SettingsWindow.axaml`**

Bind `IsEnabled="{Binding IsSupported}"` to CheckBox and add "Auto-detect" Button:
```xml
<StackPanel Spacing="5" Margin="0,10,0,0">
    <Grid ColumnDefinitions="*, Auto" Margin="5,0,0,10">
        <TextBlock Text="Hardware Acceleration"
                   FontSize="18"
                   FontWeight="Bold"
                   Foreground="{DynamicResource SystemAccentColor}"
                   VerticalAlignment="Center"/>
        <Button Grid.Column="1"
                Content="Auto-detect"
                Command="{Binding AutoDetectHardwareCommand}"
                Background="{DynamicResource SystemAccentColor}"
                Foreground="Black"
                FontWeight="SemiBold"
                FontSize="12"
                Padding="10,4"
                CornerRadius="4"
                Cursor="Hand"/>
    </Grid>

    <Border Background="{DynamicResource SecondaryBackground}"
            BorderBrush="{DynamicResource BorderColor}"
            BorderThickness="1"
            CornerRadius="6"
            Padding="14,10">

        <StackPanel Spacing="6">
            <TextBlock Text="Enable specific hardware encoders for your GPU:"
                       FontSize="12"
                       Foreground="{DynamicResource SecondaryText}"
                       Margin="0,0,0,6"/>

            <ItemsControl ItemsSource="{Binding EncoderOptions}">
                <ItemsControl.ItemTemplate>
                    <DataTemplate>
                        <CheckBox Content="{Binding Name}"
                                  IsChecked="{Binding IsIncluded}"
                                  IsEnabled="{Binding IsSupported}"
                                  FontSize="12.5"
                                  Foreground="{DynamicResource MainText}"
                                  Margin="0,3"
                                  Cursor="Hand"/>
                    </DataTemplate>
                </ItemsControl.ItemTemplate>
            </ItemsControl>
        </StackPanel>
    </Border>
</StackPanel>
```

- [ ] **Step 4: Verify build**

Run: `dotnet build "Video Size Optimizer/Video Size Optimizer.csproj"`
Expected: Build succeeded with 0 Errors.

- [ ] **Step 5: Commit**

```bash
git add "Video Size Optimizer/ViewModels/SettingsViewModel.cs" "Video Size Optimizer/Views/SettingsWindow.axaml"
git commit -m "feat(settings): update SettingsViewModel and SettingsWindow with IsSupported binding and Auto-detect button"
```

---

### Task 4: Integrate Startup Hardware Detection in MainWindowViewModel

**Files:**
- Modify: `Video Size Optimizer/ViewModels/MainWindowViewModel.cs:128-144`

**Interfaces:**
- Consumes: `GlobalSettings.HasDetectedHardware`, `FfmpegService.DetectSupportedHardwareEncodersAsync()`, `SettingsService.SaveSettingsAsync()`
- Produces: Dynamic initialization of `GlobalSettings.SupportedHardwareEncoders` and `GlobalSettings.EnabledEncoders` on first launch.

- [ ] **Step 1: Add startup detection logic to `MainWindowViewModel.cs`**

In `MainWindowViewModel` constructor, right after loading `GlobalSettings`:
```csharp
GlobalSettings = _settingsService.LoadSettings();
SelectedOutputFormat = GlobalSettings.DefaultOutputFormat;
LogService.Instance.Log("Global settings loaded.");

if (!GlobalSettings.HasDetectedHardware && DependencyChecker.CheckBinaries(out _))
{
    _ = Task.Run(async () =>
    {
        try
        {
            LogService.Instance.Log("Running initial hardware detection...", LogLevel.Info, "Hardware");
            var detectedEncoders = await _ffmpegService.DetectSupportedHardwareEncodersAsync();
            
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                GlobalSettings.SupportedHardwareEncoders = detectedEncoders;
                var enabled = new List<string> { "Standard (Slow, Best Quality)" };
                enabled.AddRange(detectedEncoders);
                GlobalSettings.EnabledEncoders = enabled;
                GlobalSettings.HasDetectedHardware = true;

                OnPropertyChanged(nameof(AvailableEncoders));
                if (!AvailableEncoders.Contains(SelectedEncoder))
                {
                    SelectedEncoder = AvailableEncoders.First();
                }

                await _settingsService.SaveSettingsAsync(GlobalSettings);
                LogService.Instance.Log($"Hardware detection complete. Supported hardware encoders: {string.Join(", ", detectedEncoders)}", LogLevel.Success, "Hardware");
            });
        }
        catch (Exception ex)
        {
            LogService.Instance.Log($"Initial hardware detection failed: {ex.Message}", LogLevel.Error, "Hardware");
        }
    });
}
```

- [ ] **Step 2: Verify build**

Run: `dotnet build "Video Size Optimizer/Video Size Optimizer.csproj"`
Expected: Build succeeded with 0 Errors.

- [ ] **Step 3: Run project build and sanity check**

Run: `dotnet build "Video Size Optimizer/Video Size Optimizer.csproj"`

- [ ] **Step 4: Commit**

```bash
git add "Video Size Optimizer/ViewModels/MainWindowViewModel.cs"
git commit -m "feat(app): add initial startup hardware auto-detection to MainWindowViewModel"
```

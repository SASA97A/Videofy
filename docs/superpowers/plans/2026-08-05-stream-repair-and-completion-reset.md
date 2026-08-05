# Stream Repair Enhancements & Completion Reset Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix cancellation status bugs (so stopping mid-work doesn't mark files completed), implement a "Reset Status" feature (toolbar, DataGrid context menu, Edit menu) to reset completed files to Ready across tabs, and add Dual-Mode Stream Repair (Instant Lossless Keyframe Remux vs Transcode Reconstruction).

**Architecture:** 
1. Add `ResetSelectedStatusCommand` in `MainWindowViewModel.cs` and expose in UI (`MainWindow.axaml`).
2. Fix mid-work stop status handling in `MainWindowViewModel.cs` so cancelled jobs reset `IsCompleted = false`.
3. Add `RepairMode` dropdown property in `MainWindowViewModel.cs` (`Lossless Keyframe Remux` vs `Transcode Reconstruction`).
4. Update `FfmpegService.cs` `RepairVideoAsync` to handle both Lossless Keyframe Remux (`-c copy`) and Transcode Reconstruction (`-c:v <encoder>`).

**Tech Stack:** C# 12, .NET 8, Avalonia UI 11, CommunityToolkit.MVVM, FFmpeg CLI.

## Global Constraints
- Build command: `dotnet build "Video Size Optimizer/Video Size Optimizer.csproj"`
- Code conventions: Standard Videofy MVVM architecture, CommunityToolkit.Mvvm source generators.

---

### Task 1: Update `FfmpegService.cs` to Support Dual-Mode Repair

**Files:**
- Modify: `Video Size Optimizer/Services/FfmpegService.cs`

**Interfaces:**
- Produces: `public async Task RepairVideoAsync(string input, string output, string encoder, bool isLosslessRemux, string trimArgs, IProgress<ConversionProgress>? progress = null)`

- [ ] **Step 1: Edit `FfmpegService.cs`**

Update `RepairVideoAsync` in `FfmpegService.cs` to accept `bool isLosslessRemux`:
```csharp
public async Task RepairVideoAsync(string input, string output, string encoder, bool isLosslessRemux, string trimArgs, IProgress<ConversionProgress>? progress = null)
{
    try
    {
        string repairFlags = "-fflags +genpts+discardcorrupt -err_detect ignore_err -analyzeduration 200M -probesize 200M";
        string args;

        if (isLosslessRemux)
        {
            // Lossless Keyframe Remux mode (-c copy)
            args = $"-hide_banner -y {repairFlags} {trimArgs} -i \"{input}\" -map 0 -c copy -avoid_negative_ts make_zero \"{output}\"";
        }
        else
        {
            // Transcode Reconstruction mode
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

            string streamMaps = "-map 0:v:0 -map 0:a? -map 0:s?";
            string videoEncoderArgs = $"-c:v {encoder} {vopts} -pix_fmt yuv420p";
            string audioEncoderArgs = "-c:a aac -b:a 192k -ar 48000 -af aresample=async=1000";
            string timingFlags = "-fps_mode cfr -movflags +faststart -max_interleave_delta 0 -avoid_negative_ts make_zero";

            args = $"-hide_banner -y {repairFlags} {trimArgs} -i \"{input}\" {streamMaps} {videoEncoderArgs} {audioEncoderArgs} {subCodec} {timingFlags} \"{output}\"";
        }

        await RunFfmpegProcessAsync(args, progress);
    }
    catch (Exception ex)
    {
        LogService.Instance.Log($"Stream repair failed. Input={input} | Output={output} | Error: {ex.Message}", LogLevel.Error, "FFMPEG");
        throw;
    }
}
```

- [ ] **Step 2: Build project**

Run: `dotnet build "Video Size Optimizer/Video Size Optimizer.csproj"`
Expected: Build succeeded with 0 errors.

- [ ] **Step 3: Commit Task 1**

```bash
git add "Video Size Optimizer/Services/FfmpegService.cs"
git commit -m "feat: add dual-mode repair support (lossless vs transcode) in FfmpegService"
```

---

### Task 2: Implement Reset Status Command and Fix Mid-Work Stop Bug in `MainWindowViewModel.cs`

**Files:**
- Modify: `Video Size Optimizer/ViewModels/MainWindowViewModel.cs`

**Interfaces:**
- Produces: `[RelayCommand] public void ResetSelectedStatus()`, `RepairModeOptions`, `RepairMode`, `IsLosslessRepairMode`

- [ ] **Step 1: Edit `MainWindowViewModel.cs`**

1. Add properties for Repair Mode and Repair Mode choices:
```csharp
public List<string> RepairModeOptions { get; } = new()
{
    "Lossless Keyframe Remux (Instant & Recommended)",
    "Transcode Reconstruction (Full Re-encode)"
};

[ObservableProperty]
[NotifyPropertyChangedFor(nameof(IsLosslessRepairMode))]
private string _selectedRepairMode = "Lossless Keyframe Remux (Instant & Recommended)";

public bool IsLosslessRepairMode => SelectedRepairMode.StartsWith("Lossless Keyframe");
```

2. Add `ResetSelectedStatus` command:
```csharp
[RelayCommand]
public void ResetSelectedStatus()
{
    var targets = Videos.Where(v => v.IsSelected).ToList();
    if (targets.Count == 0 && Videos.Count > 0)
    {
        targets = Videos.ToList();
    }

    foreach (var video in targets)
    {
        video.IsCompleted = false;
        video.IsProcessing = false;
        video.Progress = 0;

        var index = DisplayedVideos.IndexOf(video);
        if (index != -1)
        {
            DisplayedVideos[index] = video;
        }
    }

    LogService.Instance.Log($"Reset status for {targets.Count} video(s) to Ready.", LogLevel.Info, "Main");
}
```

3. Update repair execution in `StartCompressionAsync`:
```csharp
else if (SelectedTabIndex == 4)
{
    finalOutputPath = _fileService.GenerateRepairOutputPath(video.FilePath, RepairTargetFormat);

    if (!AppConstants.EncoderMap.TryGetValue(RepairSelectedEncoder, out string? repairEncoderValue))
        repairEncoderValue = "libx264";

    StatusMessage = IsLosslessRepairMode ? "Remuxing Stream..." : "Repairing Stream...";
    await _ffmpegService.RepairVideoAsync(video.FilePath, finalOutputPath, repairEncoderValue, IsLosslessRepairMode, trimArgs, p);

    LogService.Instance.Log($"Stream repair completed for {video.FileName} -> {finalOutputPath}");
}
```
Remove `video.IsCompleted = true;` from inside `SelectedTabIndex == 4` block so it is handled uniformly after checking `if (!IsBusy)`.

4. Update cancellation / exception handling:
In `catch (Exception ex)` and `finally`:
Ensure if job is cancelled (`!IsBusy`) or throws error:
```csharp
video.IsProcessing = false;
if (!video.IsCompleted)
{
    video.Progress = 0;
}
```

- [ ] **Step 2: Build project**

Run: `dotnet build "Video Size Optimizer/Video Size Optimizer.csproj"`
Expected: Build succeeded with 0 errors.

- [ ] **Step 3: Commit Task 2**

```bash
git add "Video Size Optimizer/ViewModels/MainWindowViewModel.cs"
git commit -m "feat: add ResetSelectedStatus command and fix mid-work stop cancellation bug"
```

---

### Task 3: Update `RepairView.axaml` with Repair Mode Selector

**Files:**
- Modify: `Video Size Optimizer/Views/RepairView.axaml`

- [ ] **Step 1: Edit `RepairView.axaml`**

Add ComboBox for `RepairModeOptions` / `SelectedRepairMode` in `RepairView.axaml`, and bind `IsEnabled="{Binding !IsLosslessRepairMode}"` to `RepairSelectedEncoder` so encoder is only enabled when in Transcode Reconstruction mode.

- [ ] **Step 2: Build project**

Run: `dotnet build "Video Size Optimizer/Video Size Optimizer.csproj"`
Expected: Build succeeded with 0 errors.

- [ ] **Step 3: Commit Task 3**

```bash
git add "Video Size Optimizer/Views/RepairView.axaml"
git commit -m "feat: add RepairMode selector and conditional encoder binding in RepairView"
```

---

### Task 4: Expose Reset Status Button in UI (`MainWindow.axaml`)

**Files:**
- Modify: `Video Size Optimizer/Views/MainWindow.axaml`

- [ ] **Step 1: Edit `MainWindow.axaml`**

1. Top Toolbar: Add "Reset Status" button next to *Refresh List*.
2. Edit Menu: Add `<MenuItem Header="_Reset Status to Ready" Command="{Binding ResetSelectedStatusCommand}" IsEnabled="{Binding !IsBusy}" />`.
3. DataGrid Context Menu: Add `<MenuItem Header="Reset Status to Ready" Command="{Binding $parent[DataGrid].DataContext.ResetSelectedStatusCommand}" />`.

- [ ] **Step 2: Build project**

Run: `dotnet build "Video Size Optimizer/Video Size Optimizer.csproj"`
Expected: Build succeeded with 0 errors.

- [ ] **Step 3: Commit Task 4**

```bash
git add "Video Size Optimizer/Views/MainWindow.axaml"
git commit -m "feat: expose Reset Status action in top toolbar, Edit menu, and DataGrid context menu"
```

---

### Task 5: Final Verification & Code Review
1. Build verification: `dotnet build "Video Size Optimizer/Video Size Optimizer.csproj"`.
2. Inspect `git diff` to ensure no unexpected side effects.

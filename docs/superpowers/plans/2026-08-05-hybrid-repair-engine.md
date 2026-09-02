# Hybrid Split-and-Merge Repair Engine Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement a 3-step Hybrid Repair pipeline in Videofy that splits corrupted transport streams into keyframe-aligned segments (resetting broken PTS timeline clocks), cleans up broken packets, and losslessly merges them back into a single seamless output file (`video_repaired.mp4`).

**Architecture:** Extend `FfmpegService.cs` with `RepairVideoHybridAsync`, add `UseHybridRepairMode` property in `MainWindowViewModel.cs`, and add the Hybrid Repair checkbox to `RepairView.axaml`.

**Tech Stack:** C# 12, .NET 8, Avalonia UI 11, CommunityToolkit.MVVM, FFmpeg CLI.

## Global Constraints
- Target Framework: .NET 8.0 / Avalonia 11
- Build command: `dotnet build "Video Size Optimizer/Video Size Optimizer.csproj"`
- Code conventions: Standard Videofy MVVM architecture, CommunityToolkit.Mvvm source generators.

---

### Task 1: Add `RepairVideoHybridAsync` in `FfmpegService.cs`

**Files:**
- Modify: `Video Size Optimizer/Services/FfmpegService.cs`

**Interfaces:**
- Produces: `public async Task RepairVideoHybridAsync(string input, string output, string trimArgs, IProgress<ConversionProgress>? progress = null)`

- [ ] **Step 1: Edit `FfmpegService.cs`**

Add `RepairVideoHybridAsync` to `FfmpegService.cs`:
```csharp
public async Task RepairVideoHybridAsync(string input, string output, string trimArgs, IProgress<ConversionProgress>? progress = null)
{
    string tempDir = Path.Combine(Path.GetTempPath(), $"hybrid_repair_{Guid.NewGuid():N}");
    Directory.CreateDirectory(tempDir);

    try
    {
        LogService.Instance.Log("Hybrid Repair Step 1/2: Splitting into keyframe-aligned segments (-reset_timestamps 1)...", LogLevel.Info, "HYBRID_REPAIR");

        string chunkPattern = Path.Combine(tempDir, "chunk_%03d.mp4");
        string repairFlags = "-fflags +genpts+discardcorrupt -err_detect ignore_err -analyzeduration 200M -probesize 200M";
        string segmentFlags = "-f segment -segment_time 60 -reset_timestamps 1 -segment_format_options movflags=+faststart";

        var splitArgs = $"-hide_banner -y {repairFlags} {trimArgs} -i \"{input}\" -map 0 -c copy {segmentFlags} \"{chunkPattern}\"";

        // Step 1: Split into keyframe segments
        await RunFfmpegProcessAsync(splitArgs, progress);

        // Step 2: Collect valid non-zero chunk files
        var chunks = Directory.EnumerateFiles(tempDir, "chunk_*.mp4")
                             .Select(f => new FileInfo(f))
                             .Where(fi => fi.Exists && fi.Length > 0)
                             .OrderBy(fi => fi.Name)
                             .Select(fi => fi.FullName)
                             .ToList();

        if (chunks.Count == 0)
        {
            throw new InvalidOperationException("Hybrid repair failed: No valid keyframe segments could be extracted from input file.");
        }

        LogService.Instance.Log($"Hybrid Repair Step 2/2: Concatenating {chunks.Count} valid segments into final file...", LogLevel.Info, "HYBRID_REPAIR");

        string concatListFile = Path.Combine(tempDir, "concat_list.txt");
        using (var writer = new StreamWriter(concatListFile, false, new System.Text.UTF8Encoding(false)))
        {
            foreach (var chunk in chunks)
            {
                string safePath = chunk.Replace("'", "'\\''");
                writer.WriteLine($"file '{safePath}'");
            }
        }

        var concatArgs = $"-hide_banner -y -f concat -safe 0 -i \"{concatListFile}\" -c copy -movflags +faststart \"{output}\"";
        await RunFfmpegProcessAsync(concatArgs, progress);

        LogService.Instance.Log($"Hybrid Repair complete -> {output}", LogLevel.Success, "HYBRID_REPAIR");
    }
    catch (Exception ex)
    {
        LogService.Instance.Log($"Hybrid Repair failed | Input={input} | Output={output} | Error: {ex.Message}", LogLevel.Error, "HYBRID_REPAIR");
        throw;
    }
    finally
    {
        try
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
        catch (Exception ex)
        {
            LogService.Instance.Log($"Failed to delete temp hybrid repair directory | {ex.Message}", LogLevel.Warning, "HYBRID_REPAIR");
        }
    }
}
```

- [ ] **Step 2: Build project**

Run: `dotnet build "Video Size Optimizer/Video Size Optimizer.csproj"`
Expected: Build succeeded with 0 errors.

- [ ] **Step 3: Commit Task 1**

```bash
git add "Video Size Optimizer/Services/FfmpegService.cs"
git commit -m "feat: add RepairVideoHybridAsync pipeline in FfmpegService"
```

---

### Task 2: Integrate `UseHybridRepairMode` in `MainWindowViewModel.cs`

**Files:**
- Modify: `Video Size Optimizer/ViewModels/MainWindowViewModel.cs`

- [ ] **Step 1: Edit `MainWindowViewModel.cs`**

1. Add property:
```csharp
[ObservableProperty] private bool _useHybridRepairMode = true;
```

2. Update `SelectedTabIndex == 4` batch loop execution in `StartCompressionAsync`:
```csharp
else if (SelectedTabIndex == 4)
{
    finalOutputPath = _fileService.GenerateRepairOutputPath(video.FilePath, RepairTargetFormat);

    if (!AppConstants.EncoderMap.TryGetValue(RepairSelectedEncoder, out string? repairEncoderValue))
        repairEncoderValue = "libx264";

    if (UseHybridRepairMode)
    {
        StatusMessage = "Hybrid Repairing Stream...";
        await _ffmpegService.RepairVideoHybridAsync(video.FilePath, finalOutputPath, trimArgs, p);
    }
    else
    {
        StatusMessage = IsLosslessRepairMode ? "Remuxing Stream..." : "Repairing Stream...";
        await _ffmpegService.RepairVideoAsync(video.FilePath, finalOutputPath, repairEncoderValue, IsLosslessRepairMode, trimArgs, p);
    }

    LogService.Instance.Log($"Stream repair completed for {video.FileName} -> {finalOutputPath}");
}
```

- [ ] **Step 2: Build project**

Run: `dotnet build "Video Size Optimizer/Video Size Optimizer.csproj"`
Expected: Build succeeded with 0 errors.

- [ ] **Step 3: Commit Task 2**

```bash
git add "Video Size Optimizer/ViewModels/MainWindowViewModel.cs"
git commit -m "feat: integrate UseHybridRepairMode into MainWindowViewModel"
```

---

### Task 3: Add Hybrid Repair Checkbox in `RepairView.axaml`

**Files:**
- Modify: `Video Size Optimizer/Views/RepairView.axaml`

- [ ] **Step 1: Edit `RepairView.axaml`**

Add CheckBox to `RepairView.axaml`:
```xml
<CheckBox IsChecked="{Binding UseHybridRepairMode}"
          IsEnabled="{Binding !IsBusy}"
          Cursor="Hand"
          Margin="0,4,0,0">
    <StackPanel Orientation="Horizontal" Spacing="6">
        <TextBlock Text="☑ Enable Hybrid Split-and-Merge Repair Engine (Recommended)" Foreground="{DynamicResource MainText}" FontSize="12" FontWeight="Bold"/>
        <Border Background="{DynamicResource InputBackground}" BorderBrush="{DynamicResource BorderColor}" BorderThickness="1" CornerRadius="10" Width="16" Height="16" ToolTip.Tip="Splits damaged video into keyframe chunks first (resetting corrupt PTS timeline clocks), then losslessly merges them back into a single seamless repaired file. Far superior for damaged .ts &amp; live recordings.">
            <TextBlock Text="?" Foreground="{DynamicResource SystemAccentColor}" FontSize="11" FontWeight="Bold" HorizontalAlignment="Center" VerticalAlignment="Center"/>
        </Border>
    </StackPanel>
</CheckBox>
```

- [ ] **Step 2: Build project**

Run: `dotnet build "Video Size Optimizer/Video Size Optimizer.csproj"`
Expected: Build succeeded with 0 errors.

- [ ] **Step 3: Commit Task 3**

```bash
git add "Video Size Optimizer/Views/RepairView.axaml"
git commit -m "feat: add Hybrid Repair Checkbox in RepairView"
```

---

### Task 4: Final Verification & Code Review
1. Build check: `dotnet build "Video Size Optimizer/Video Size Optimizer.csproj"`.
2. Code diff review.

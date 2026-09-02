# Design: Merge Tab Encoder Selection

**Date:** 2026-08-05
**Scope:** Add an encoder dropdown to the merge tab so users can select GPU or CPU encoders for merge re-encoding, plus fix the `-vcodec` / `-c:v` inconsistency in `CompressAsync()`.

## Problem

The merge tab currently has no encoder selector. When re-encoding is needed (incompatible streams or force-reencode enabled), it silently reuses `SelectedEncoder` from the compression tab. The user has no way to control which encoder is used for merging without switching tabs. Additionally, the compression service uses `-vcodec` for GPU encoders but `-c:v` for CPU — both are valid FFmpeg aliases but `-c:v` is the modern standard and should be used consistently.

## Changes

### 1. ViewModel: `MainWindowViewModel.cs`

**Add property** (alongside existing merge properties near line 1389):

```csharp
[ObservableProperty] private string _mergeSelectedEncoder = "Standard (Slow, Best Quality)";
```

This reuses `AvailableEncoders` (line 57, backed by `GlobalSettings.EnabledEncoders`) as its `ItemsSource` — no new data source needed. The user's enabled/detected encoders from Settings apply to both tabs.

**Update merge execution** (line 577-578): Replace `SelectedEncoder` with `MergeSelectedEncoder`:

```csharp
if (!AppConstants.EncoderMap.TryGetValue(MergeSelectedEncoder, out string? encoderValue))
    encoderValue = "libx264";
```

### 2. View: `MergeView.axaml`

Add an Encoder ComboBox inside the existing horizontal `StackPanel` (line 27), before the Target Format dropdown:

```xml
<StackPanel Orientation="Horizontal" Spacing="8">
    <TextBlock Text="Encoder:" VerticalAlignment="Center" FontSize="12"
               Foreground="{DynamicResource SecondaryText}"/>
    <ComboBox ItemsSource="{Binding AvailableEncoders}"
              SelectedItem="{Binding MergeSelectedEncoder}"
              IsEnabled="{Binding !IsBusy}"
              Background="{DynamicResource InputBackground}"
              BorderBrush="{DynamicResource BorderColor}"
              Width="170" FontSize="12" Cursor="Hand"/>
</StackPanel>
```

### 3. Bug Fix: `FfmpegService.cs`

In `CompressAsync()`, normalize `-vcodec` to `-c:v` on three lines (151, 155, 159).

### 4. Release Notes: `releasenotes.md`

Added entries for merge encoder selection feature and `-vcodec` fix.

## Files Changed

| File | Type | Description |
|------|------|-------------|
| `ViewModels/MainWindowViewModel.cs` | Feature | New `MergeSelectedEncoder` property; updated merge execution |
| `Views/MergeView.axaml` | Feature | Encoder ComboBox added to merge settings |
| `Services/FfmpegService.cs` | Bug fix | `-vcodec` -> `-c:v` normalization |
| `releasenotes.md` | Docs | New feature + fix entries |

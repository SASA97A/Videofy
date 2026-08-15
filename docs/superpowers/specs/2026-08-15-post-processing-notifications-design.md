# Cross-Platform Post-Processing Notifications & Audio Chime Design Spec

## Overview
When long-running video processing batches complete in Videofy, users working in other applications may miss the completion popup. This feature adds cross-platform OS desktop notifications and audio chimes upon batch completion, detailing the number of processed videos and total disk space saved.

## Data Model & Settings (`AppSettings.cs` & `SettingsViewModel.cs`)
Add two boolean properties to `AppSettings`:
- `PlaySoundOnCompletion` (bool, default: `true`): Plays an audio chime when batch compression completes.
- `SendDesktopNotification` (bool, default: `true`): Sends an OS native desktop notification when batch compression completes.

Expose these in `SettingsViewModel.cs` as observable properties:
- `[ObservableProperty] private bool _playSoundOnCompletion;`
- `[ObservableProperty] private bool _sendDesktopNotification;`

Add UI checkboxes in `SettingsWindow.axaml` under **Notification & Dialog Settings**:
- Checkbox: `Play audio chime when batch processing completes`
- Checkbox: `Send OS desktop notification when batch processing completes`

## Cross-Platform Service Implementation (`SystemUtilityService.cs`)

### 1. OS Native Desktop Notifications (`SendDesktopNotificationAsync`)
- **Windows**: Invoke PowerShell Toast notification snippet via process execution (`New-Object -ComObject WScript.Shell...` or `powershell -Command "[reflection.assembly]::loadwithpartialname('System.Windows.Forms'); $n = new-object System.Windows.Forms.NotifyIcon; ..."`).
- **macOS**: Execute `osascript -e 'display notification "{message}" with title "{title}"'`.
- **Linux**: Execute `notify-send "{title}" "{message}"`.

### 2. Audio Chime (`PlayCompletionSoundAsync`)
- **Windows**: Invoke `System.Media.SystemSounds.Asterisk.Play()`.
- **macOS**: Execute `afplay /System/Library/Sounds/Glass.aiff` or `osascript -e 'beep'`.
- **Linux**: Execute `paplay`, `canberra-gtk-play`, or `Console.Beep()`.

## Batch Completion Integration (`MainWindowViewModel.cs`)
In `MainWindowViewModel.cs` inside `ProcessQueueAsync` when `completedCount > 0`:
```csharp
if (GlobalSettings.SendDesktopNotification)
{
    _ = _systemService.SendDesktopNotificationAsync(
        "Task Completed - Videofy",
        $"Successfully processed {completedCount} videos.\nTotal space saved: {sizeDisplay}");
}

if (GlobalSettings.PlaySoundOnCompletion)
{
    _ = _systemService.PlayCompletionSoundAsync();
}
```

## Release Notes Update (`releasenotes.md`)
Under `## New Features`:
```markdown
- **Cross-Platform OS Notifications & Audio Chimes:** Videofy now sends native desktop notifications and plays audio chimes upon batch completion on Windows, macOS, and Linux, displaying the total number of processed videos and space saved.
```

## Verification
1. `dotnet build` succeeds with 0 errors.
2. Verify notification and chime settings in `SettingsWindow.axaml`.

# Post-Processing Notifications & Audio Chimes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Provide cross-platform OS desktop notifications and audio chimes upon batch video processing completion.

**Architecture:** Add `PlaySoundOnCompletion` and `SendDesktopNotification` settings to `AppSettings`, expose them in `SettingsViewModel` and `SettingsWindow.axaml`, implement `SendDesktopNotificationAsync` and `PlayCompletionSoundAsync` in `SystemUtilityService.cs`, and trigger them in `MainWindowViewModel.cs` when batch jobs complete.

**Tech Stack:** C#, Avalonia UI, System.Media / Process utilities for Windows/macOS/Linux.

## Global Constraints

- Settings default: `true` for both sound chime and desktop notification.
- Branch: `feature/post-processing-notifications` off `Version-1.4.3`.

---

### Task 1: Add Notification Settings to AppSettings, ViewModel, and Settings UI

**Files:**
- Modify: `Video Size Optimizer/Models/AppSettings.cs:20-22`
- Modify: `Video Size Optimizer/ViewModels/SettingsViewModel.cs:30-31, 44-46, 97-99`
- Modify: `Video Size Optimizer/Views/SettingsWindow.axaml:160-185`

**Interfaces:**
- Consumes: `AppSettings`, `SettingsViewModel`, `SettingsWindow.axaml`.
- Produces: `PlaySoundOnCompletion` and `SendDesktopNotification` properties and UI checkboxes in Global Settings.

- [ ] **Step 1: Update `AppSettings.cs`**

Add properties to `Video Size Optimizer/Models/AppSettings.cs`:

```csharp
public bool PlaySoundOnCompletion { get; set; } = true;
public bool SendDesktopNotification { get; set; } = true;
```

- [ ] **Step 2: Update `SettingsViewModel.cs`**

Add observable properties to `Video Size Optimizer/ViewModels/SettingsViewModel.cs`:

```csharp
[ObservableProperty] private bool _playSoundOnCompletion;
[ObservableProperty] private bool _sendDesktopNotification;
```

In constructor:
```csharp
PlaySoundOnCompletion = currentSettings.PlaySoundOnCompletion;
SendDesktopNotification = currentSettings.SendDesktopNotification;
```

In `GetUpdatedSettings()`:
```csharp
PlaySoundOnCompletion = PlaySoundOnCompletion,
SendDesktopNotification = SendDesktopNotification,
```

- [ ] **Step 3: Update `SettingsWindow.axaml`**

Add checkboxes inside the Notification & Dialog Settings card box in `Video Size Optimizer/Views/SettingsWindow.axaml`:

```xml
							<CheckBox IsChecked="{Binding SendDesktopNotification}"
									  Cursor="Hand"
									  Content="Send OS desktop notification when batch processing completes"
									  FontSize="13"
									  Foreground="{DynamicResource MainText}"
									  ToolTip.Tip="When enabled, sends a native desktop notification showing processed videos count and space saved."/>

							<CheckBox IsChecked="{Binding PlaySoundOnCompletion}"
									  Cursor="Hand"
									  Content="Play audio chime when batch processing completes"
									  FontSize="13"
									  Foreground="{DynamicResource MainText}"
									  ToolTip.Tip="When enabled, plays a system audio sound when all queued videos complete processing."/>
```

- [ ] **Step 4: Build project and verify compilation**

Run: `dotnet build "Video Size Optimizer/Video Size Optimizer.csproj"`
Expected: Build succeeded with 0 Errors.

- [ ] **Step 5: Commit**

```bash
git add "Video Size Optimizer/Models/AppSettings.cs" "Video Size Optimizer/ViewModels/SettingsViewModel.cs" "Video Size Optimizer/Views/SettingsWindow.axaml"
git commit -m "feat: add PlaySoundOnCompletion and SendDesktopNotification to settings and UI"
```

---

### Task 2: Implement Cross-Platform Notifications & Audio Service

**Files:**
- Modify: `Video Size Optimizer/Services/SystemUtilityService.cs:280-285`

**Interfaces:**
- Consumes: System processes (`Process.Start`), `RuntimeInformation`, `System.Media.SystemSounds`.
- Produces: `Task SendDesktopNotificationAsync(string title, string message)` and `Task PlayCompletionSoundAsync()`.

- [ ] **Step 1: Update `SystemUtilityService.cs`**

Add notification and audio methods to `Video Size Optimizer/Services/SystemUtilityService.cs`:

```csharp
public async Task SendDesktopNotificationAsync(string title, string message)
{
    await Task.Run(() =>
    {
        try
        {
            bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            bool isMac = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
            bool isLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

            if (isWindows)
            {
                string script = $"[reflection.assembly]::loadwithpartialname('System.Windows.Forms'); $n = new-object System.Windows.Forms.NotifyIcon; $n.Icon = [System.Drawing.SystemIcons]::Information; $n.Visible = $true; $n.ShowBalloonTip(5000, '{title.Replace("'", "''")}', '{message.Replace("'", "''").Replace("\n", " ") }', [System.Windows.Forms.ToolTipIcon]::Info)";
                Process.Start(new ProcessStartInfo
                {
                    FileName = "powershell",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{script}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
            }
            else if (isMac)
            {
                string cleanMsg = message.Replace("\"", "\\\"").Replace("\n", " ");
                string cleanTitle = title.Replace("\"", "\\\"");
                Process.Start("osascript", $"-e \"display notification \\\"{cleanMsg}\\\" with title \\\"{cleanTitle}\\\"[rtk:truncated 26 lines]
        }
        catch (Exception ex)
        {
            LogService.Instance.Log($"Desktop notification error: {ex.Message}", LogLevel.Error, "SysUtil");
        }
    });
}

public async Task PlayCompletionSoundAsync()
{
    await Task.Run(() =>
    {
        try
        {
            bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            bool isMac = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
            bool isLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

            if (isWindows)
            {
                if (OperatingSystem.IsWindows())
                {
                    System.Media.SystemSounds.Asterisk.Play();
                }
            }
            else if (isMac)
            {
                Process.Start("afplay", "/System/Library/Sounds/Glass.aiff");
            }
            else if (isLinux)
            {
                Process.Start("paplay", "/usr/share/sounds/freedesktop/stereo/complete.oga");
            }
        }
        catch (Exception ex)
        {
            LogService.Instance.Log($"Audio chime error: {ex.Message}", LogLevel.Error, "SysUtil");
        }
    });
}
```

- [ ] **Step 2: Build project and verify compilation**

Run: `dotnet build "Video Size Optimizer/Video Size Optimizer.csproj"`
Expected: Build succeeded with 0 Errors.

- [ ] **Step 3: Commit**

```bash
git add "Video Size Optimizer/Services/SystemUtilityService.cs"
git commit -m "feat: implement cross-platform desktop notifications and audio chimes in SystemUtilityService"
```

---

### Task 3: Trigger Notifications and Sound on Batch Completion

**Files:**
- Modify: `Video Size Optimizer/ViewModels/MainWindowViewModel.cs:1025-1033`

**Interfaces:**
- Consumes: `GlobalSettings.SendDesktopNotification`, `GlobalSettings.PlaySoundOnCompletion`, `SystemUtilityService`.
- Produces: Automated notifications and sound playback when batch jobs finish.

- [ ] **Step 1: Update `MainWindowViewModel.cs`**

Modify `MainWindowViewModel.cs` inside `ProcessQueueAsync` when `completedCount > 0`:

```csharp
                if (GlobalSettings.SendDesktopNotification)
                {
                    _ = _systemService.SendDesktopNotificationAsync(
                        "Task Completed - Videofy",
                        $"Successfully processed {completedCount} video(s).\nTotal space saved: {sizeDisplay}");
                }

                if (GlobalSettings.PlaySoundOnCompletion)
                {
                    _ = _systemService.PlayCompletionSoundAsync();
                }

                await _messageService.ShowSuccessAsync("Task Completed",
                       $"Successfully processed {completedCount} videos.\n\nTotal space saved: {sizeDisplay}");
```

- [ ] **Step 2: Build project and verify compilation**

Run: `dotnet build "Video Size Optimizer/Video Size Optimizer.csproj"`
Expected: Build succeeded with 0 Errors.

- [ ] **Step 3: Commit**

```bash
git add "Video Size Optimizer/ViewModels/MainWindowViewModel.cs"
git commit -m "feat: trigger desktop notification and audio chime on batch completion"
```

---

### Task 4: Release Notes & Branch Merge

**Files:**
- Modify: `releasenotes.md:3-5`

**Interfaces:**
- Consumes: Release notes file structure.
- Produces: Updated release documentation and clean merge to `Version-1.4.3`.

- [ ] **Step 1: Edit `releasenotes.md`**

Add entry under `## New Features`:

```markdown
- **Cross-Platform OS Desktop Notifications & Audio Chimes:** Videofy now sends native desktop notifications and plays audio chimes upon batch completion on Windows, macOS, and Linux, displaying the total number of processed videos and disk space saved.
```

- [ ] **Step 2: Commit release notes**

```bash
git add releasenotes.md
git commit -m "docs: add desktop notifications and audio chimes entry to release notes"
```

- [ ] **Step 3: Perform Release build verification**

Run: `dotnet build "Video Size Optimizer/Video Size Optimizer.csproj" --configuration Release`
Expected: Build succeeded with 0 Errors.

- [ ] **Step 4: Merge feature branch into Version-1.4.3**

```bash
git checkout Version-1.4.3
git merge feature/post-processing-notifications
git branch -d feature/post-processing-notifications
```

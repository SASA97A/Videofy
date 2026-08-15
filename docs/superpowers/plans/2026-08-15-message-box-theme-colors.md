# Message Box Theme Colors Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Style all message box popups in `MessageService` with Videofy's dark theme palette (`#1E2228` secondary background and `#F8FAFC` main text) and center them over the active window.

**Architecture:** Create a `CreateParams` helper in `MessageService` that builds standardized `MessageBoxStandardParams` with theme colors and `WindowCustomizer` applied. Update `releasenotes.md` to document the UI enhancement.

**Tech Stack:** C#, Avalonia UI, MsBox.Avalonia.

## Global Constraints

- Background color: `#1E2228` (Videofy Secondary Background)
- Foreground color: `#F8FAFC` (Videofy Main Text)
- Window Startup Location: `WindowStartupLocation.CenterOwner`
- Branch: `feature/message-box-theme-colors` off `Version-1.4.3`

---

### Task 1: Add Theme Styling to MessageService

**Files:**
- Modify: `Video Size Optimizer/Services/MessageService.cs:1-97`

**Interfaces:**
- Consumes: `MsBox.Avalonia`, `Avalonia.Media.Brush`, `Avalonia.Controls.WindowStartupLocation`.
- Produces: Theme-styled message box popups across `ShowInfoAsync`, `ShowErrorAsync`, `ShowSuccessAsync`, `ShowCustomAsync`, `ShowYesNoAsync`.

- [ ] **Step 1: Update `MessageService.cs`**

Modify `Video Size Optimizer/Services/MessageService.cs` to add `CreateParams` helper and apply it to all dialog creation methods:

```csharp
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using MsBox.Avalonia;
using MsBox.Avalonia.Dto;
using MsBox.Avalonia.Enums;
using System.Threading.Tasks;
using Video_Size_Optimizer.Services;

namespace Video_Size_Optimizer
{
    public class MessageService
    {
        private readonly SettingsService _settingsService = new();

        private Window? GetMainWindow()
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                return desktop.MainWindow;
            }
            return null;
        }

        private MessageBoxStandardParams CreateParams(string title, string message, ButtonEnum buttons, Icon icon)
        {
            return new MessageBoxStandardParams
            {
                ContentTitle = title,
                ContentMessage = message + "  ",
                ButtonDefinitions = buttons,
                Icon = icon,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                WindowCustomizer = win =>
                {
                    win.Background = Brush.Parse("#1E2228");
                    win.Foreground = Brush.Parse("#F8FAFC");
                }
            };
        }

        public async Task ShowInfoAsync(string title, string message)
        {
            var settings = _settingsService.LoadSettings();
            var parameters = CreateParams(title, message, ButtonEnum.Ok, Icon.Info);
            var box = MessageBoxManager.GetMessageBoxStandard(parameters);
            var mainWindow = GetMainWindow();
            if (settings.ModalInfoMessages && mainWindow != null)
            {
                await box.ShowWindowDialogAsync(mainWindow);
            }
            else
            {
                await box.ShowAsync();
            }
        }

        public async Task ShowErrorAsync(string title, string message)
        {
            var settings = _settingsService.LoadSettings();
            var parameters = CreateParams(title, message, ButtonEnum.Ok, Icon.Error);
            var box = MessageBoxManager.GetMessageBoxStandard(parameters);
            var mainWindow = GetMainWindow();
            if (settings.ModalErrorMessages && mainWindow != null)
            {
                await box.ShowWindowDialogAsync(mainWindow);
            }
            else
            {
                await box.ShowAsync();
            }
        }

        public async Task ShowSuccessAsync(string title, string message)
        {
            var settings = _settingsService.LoadSettings();
            var parameters = CreateParams(title, message, ButtonEnum.Ok, Icon.Success);
            var box = MessageBoxManager.GetMessageBoxStandard(parameters);
            var mainWindow = GetMainWindow();
            if (settings.ModalCompletionMessages && mainWindow != null)
            {
                await box.ShowWindowDialogAsync(mainWindow);
            }
            else
            {
                await box.ShowAsync();
            }
        }

        public async Task<ButtonResult> ShowCustomAsync(MessageBoxStandardParams parameters)
        {
            if (parameters.WindowCustomizer == null)
            {
                parameters.WindowCustomizer = win =>
                {
                    win.Background = Brush.Parse("#1E2228");
                    win.Foreground = Brush.Parse("#F8FAFC");
                };
            }
            var box = MessageBoxManager.GetMessageBoxStandard(parameters);
            var mainWindow = GetMainWindow();
            if (mainWindow != null)
            {
                return await box.ShowWindowDialogAsync(mainWindow);
            }
            return await box.ShowAsync();
        }

        public async Task<bool> ShowYesNoAsync(string title, string message)
        {
            var parameters = CreateParams(title, message, ButtonEnum.YesNo, Icon.Question);
            var box = MessageBoxManager.GetMessageBoxStandard(parameters);
            var mainWindow = GetMainWindow();
            ButtonResult result;
            if (mainWindow != null)
            {
                result = await box.ShowWindowDialogAsync(mainWindow);
            }
            else
            {
                result = await box.ShowAsync();
            }
            return result == ButtonResult.Yes;
        }
    }
}
```

- [ ] **Step 2: Build project and verify compilation**

Run: `dotnet build "Video Size Optimizer/Video Size Optimizer.csproj"`
Expected: Build succeeded with 0 Errors.

- [ ] **Step 3: Commit**

```bash
git add "Video Size Optimizer/Services/MessageService.cs"
git commit -m "feat: apply Videofy dark theme colors and owner centering to message boxes"
```

---

### Task 2: Update Release Notes

**Files:**
- Modify: `releasenotes.md:14-16`

**Interfaces:**
- Consumes: Release notes documentation structure.
- Produces: Updated `releasenotes.md` under `## UI Enhancements`.

- [ ] **Step 1: Edit `releasenotes.md`**

Add the feature note under `## UI Enhancements`:

```markdown
- **Theme-Consistent Message Boxes:** Message box dialogs now follow Videofy's dark theme palette (`#1E2228` secondary background and `#F8FAFC` main text) and automatically center over the active window.
```

- [ ] **Step 2: Commit**

```bash
git add releasenotes.md
git commit -m "docs: add theme-consistent message boxes entry to release notes"
```

---

### Task 3: Final Verification & Merge

- [ ] **Step 1: Perform full Release build**

Run: `dotnet build "Video Size Optimizer/Video Size Optimizer.csproj" --configuration Release`
Expected: Build succeeded with 0 Errors.

- [ ] **Step 2: Checkout Version-1.4.3 and merge feature branch**

```bash
git checkout Version-1.4.3
git merge feature/message-box-theme-colors
git branch -d feature/message-box-theme-colors
```

- [ ] **Step 3: Verify clean build on Version-1.4.3**

Run: `dotnet build "Video Size Optimizer/Video Size Optimizer.csproj" --configuration Release`
Expected: Build succeeded with 0 Errors.

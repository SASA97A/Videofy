# Message Box Modality and Notification Settings Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Allow users to choose which message dialogs stay on screen and disable app window input (modal behavior) vs. modeless behavior, preventing batch completion and error messages from disappearing behind the app window.

**Architecture:** Add three modality flags (`ModalCompletionMessages`, `ModalErrorMessages`, `ModalInfoMessages`) to `AppSettings` and `SettingsViewModel`, add UI controls to `SettingsWindow.axaml`, and update `MessageService` to resolve the main `Window` and invoke `ShowWindowDialogAsync` or `ShowAsync` accordingly.

**Tech Stack:** C#, Avalonia UI, MsBox.Avalonia, .NET 8 / standard WPF/Avalonia patterns.

## Global Constraints

- Platform: Windows / Avalonia UI (.NET)
- Settings file path: `%AppData%/Videofy/settings.json`
- Modality settings default: `true` for all categories.
- Branch: `feature/message-modality-settings` off `Version-1.4.3`.

---

### Task 1: Update AppSettings and SettingsViewModel

**Files:**
- Modify: `Video Size Optimizer/Models/AppSettings.cs:20-21`
- Modify: `Video Size Optimizer/ViewModels/SettingsViewModel.cs:29-30, 43-44, 96-97`

**Interfaces:**
- Consumes: Existing `AppSettings` data model and `SettingsViewModel`.
- Produces: `ModalCompletionMessages`, `ModalErrorMessages`, and `ModalInfoMessages` properties on `AppSettings` and `SettingsViewModel`.

- [ ] **Step 1: Update `AppSettings.cs`**

Add properties to `Video Size Optimizer/Models/AppSettings.cs`:

```csharp
public bool ModalCompletionMessages { get; set; } = true;
public bool ModalErrorMessages { get; set; } = true;
public bool ModalInfoMessages { get; set; } = true;
```

- [ ] **Step 2: Update `SettingsViewModel.cs`**

Add observable properties and loading/saving logic to `Video Size Optimizer/ViewModels/SettingsViewModel.cs`:

```csharp
[ObservableProperty] private bool _modalCompletionMessages;
[ObservableProperty] private bool _modalErrorMessages;
[ObservableProperty] private bool _modalInfoMessages;
```

In constructor:
```csharp
ModalCompletionMessages = currentSettings.ModalCompletionMessages;
ModalErrorMessages = currentSettings.ModalErrorMessages;
ModalInfoMessages = currentSettings.ModalInfoMessages;
```

In `GetUpdatedSettings()`:
```csharp
ModalCompletionMessages = ModalCompletionMessages,
ModalErrorMessages = ModalErrorMessages,
ModalInfoMessages = ModalInfoMessages
```

- [ ] **Step 3: Build to verify no syntax errors**

Run: `dotnet build "Video Size Optimizer/Video Size Optimizer.csproj"`
Expected: Build succeeded with 0 Errors.

- [ ] **Step 4: Commit**

```bash
git add "Video Size Optimizer/Models/AppSettings.cs" "Video Size Optimizer/ViewModels/SettingsViewModel.cs"
git commit -m "feat: add modality properties to AppSettings and SettingsViewModel"
```

---

### Task 2: Update SettingsWindow UI

**Files:**
- Modify: `Video Size Optimizer/Views/SettingsWindow.axaml:167-168`

**Interfaces:**
- Consumes: `ModalCompletionMessages`, `ModalErrorMessages`, `ModalInfoMessages` from `SettingsViewModel`.
- Produces: XAML UI controls in Global Settings.

- [ ] **Step 1: Add Notification & Dialog Settings controls to `SettingsWindow.axaml`**

Add the following section before the closing `StackPanel` of `ScrollViewer`:

```xml
				<StackPanel Spacing="5" Margin="0,10,0,0">
					<TextBlock Text="Notification &amp; Dialog Settings" FontSize="18" FontWeight="Bold" Foreground="{DynamicResource SystemAccentColor}" Margin="0,0,0,5"/>

					<CheckBox IsChecked="{Binding ModalCompletionMessages}"
							  Cursor="Hand"
							  Content="Block app input &amp; keep Task Completion messages on top"
							  FontSize="13"
							  Foreground="{DynamicResource MainText}"
							  ToolTip.Tip="When enabled, batch finished &amp; task completion popups stay on top and disable app window interaction until dismissed."/>

					<CheckBox IsChecked="{Binding ModalErrorMessages}"
							  Cursor="Hand"
							  Content="Block app input &amp; keep Error &amp; Warning messages on top"
							  FontSize="13"
							  Foreground="{DynamicResource MainText}"
							  ToolTip.Tip="When enabled, error and warning popups stay on top and disable app window interaction until dismissed."/>

					<CheckBox IsChecked="{Binding ModalInfoMessages}"
							  Cursor="Hand"
							  Content="Block app input &amp; keep Informational messages on top"
							  FontSize="13"
							  Foreground="{DynamicResource MainText}"
							  ToolTip.Tip="When enabled, informational and about popups stay on top and disable app window interaction until dismissed."/>
				</StackPanel>
```

- [ ] **Step 2: Build to verify XAML compilation**

Run: `dotnet build "Video Size Optimizer/Video Size Optimizer.csproj"`
Expected: Build succeeded with 0 Errors.

- [ ] **Step 3: Commit**

```bash
git add "Video Size Optimizer/Views/SettingsWindow.axaml"
git commit -m "feat: add Notification & Dialog Settings UI to SettingsWindow"
```

---

### Task 3: Update MessageService Modality Logic

**Files:**
- Modify: `Video Size Optimizer/Services/MessageService.cs:1-42`

**Interfaces:**
- Consumes: `SettingsService.LoadSettings()`, `IClassicDesktopStyleApplicationLifetime.MainWindow`, `MsBox.Avalonia`.
- Produces: Window-dialog modal display when user settings mandate modality.

- [ ] **Step 1: Update `MessageService.cs`**

Modify `Video Size Optimizer/Services/MessageService.cs` to resolve `MainWindow` and conditionally invoke `ShowWindowDialogAsync`:

```csharp
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
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

        public async Task ShowInfoAsync(string title, string message)
        {
            var settings = _settingsService.LoadSettings();
            var box = MessageBoxManager.GetMessageBoxStandard(title, message + "  ", ButtonEnum.Ok, Icon.Info);
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
            var box = MessageBoxManager.GetMessageBoxStandard(title, message + "  ", ButtonEnum.Ok, Icon.Error);
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
            var box = MessageBoxManager.GetMessageBoxStandard(title, message + "  ", ButtonEnum.Ok, Icon.Success);
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
            var box = MessageBoxManager.GetMessageBoxStandard(title, message + "  ", ButtonEnum.YesNo, Icon.Question);
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
git commit -m "feat: implement modal dialog resolution in MessageService based on user settings"
```

---

### Task 4: Final Verification and Build

- [ ] **Step 1: Perform full project build**

Run: `dotnet build "Video Size Optimizer/Video Size Optimizer.csproj" --configuration Release`
Expected: Build succeeded with 0 Errors.

- [ ] **Step 2: Verify git status and commit history**

Run: `git status` and `git log -n 5 --oneline`
Expected: Clean working tree on branch `feature/message-modality-settings`.

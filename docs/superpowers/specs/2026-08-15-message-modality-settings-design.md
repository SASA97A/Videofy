# Message Box Modality and Notification Settings Design Spec

## Overview
Currently, message dialogs generated via `MessageService` open as unparented/modeless windows (`ShowAsync()`). As a result, when tasks complete (e.g. video compression finishing) or when warning/error popups appear, clicking on the main window moves the message box behind the main application window or minimizes it. This leaves users confused about whether background batch processing completed.

This spec details the introduction of user-configurable notification & dialog modality settings in Videofy, allowing users to control whether message popups block app input and remain on top of the main window.

## User Requirements & Scope
1. **Categorized Modality Settings**:
   - **Task / Batch Completion**: Controls whether batch/task completion dialogs block main window input and stay on top. (Default: `true`)
   - **Errors & Warnings**: Controls whether error and warning dialogs block main window input and stay on top. (Default: `true`)
   - **Information & About**: Controls whether informational and about dialogs block main window input and stay on top. (Default: `true`)
   - *(Confirmation / Question dialogs like Yes/No exit prompts will remain modal by default for system safety.)*
2. **Persistence**: Settings must save to `%AppData%/Videofy/settings.json` via `SettingsService`.
3. **UI Integration**: Add a dedicated **Notification & Dialog Settings** section in `SettingsWindow.axaml`.

## Architecture & Component Modifications

### 1. Data Model (`AppSettings.cs`)
Add three boolean properties to `AppSettings`:
```csharp
public bool ModalCompletionMessages { get; set; } = true;
public bool ModalErrorMessages { get; set; } = true;
public bool ModalInfoMessages { get; set; } = true;
```

### 2. Settings ViewModel (`SettingsViewModel.cs`)
Add observable properties and mapping logic:
- `[ObservableProperty] private bool _modalCompletionMessages;`
- `[ObservableProperty] private bool _modalErrorMessages;`
- `[ObservableProperty] private bool _modalInfoMessages;`

Initialize these fields from `AppSettings` in constructor and include them in `GetUpdatedSettings()`.

### 3. Settings View (`SettingsWindow.axaml`)
Add a new visual section in `SettingsWindow.axaml` under **Notification & Dialog Settings**:
- Checkbox: `Delete/Block app input & keep Task Completion messages on top`
- Checkbox: `Block app input & keep Error & Warning messages on top`
- Checkbox: `Block app input & keep Informational messages on top`
- Tooltips explaining how modal popups stay anchored to the app window without minimizing behind it.

### 4. Message Service (`MessageService.cs`)
Update `MessageService` to read settings via `SettingsService` and locate the active main application window via Avalonia's `IClassicDesktopStyleApplicationLifetime`:

```csharp
private Window? GetMainWindow()
{
    if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
    {
        return desktop.MainWindow;
    }
    return null;
}
```

Method behavior based on settings:
- `ShowSuccessAsync`: If `ModalCompletionMessages` is true and `mainWindow != null`, execute `await box.ShowWindowDialogAsync(mainWindow)`. Otherwise, execute `await box.ShowAsync()`.
- `ShowErrorAsync`: If `ModalErrorMessages` is true and `mainWindow != null`, execute `await box.ShowWindowDialogAsync(mainWindow)`. Otherwise, execute `await box.ShowAsync()`.
- `ShowInfoAsync`: If `ModalInfoMessages` is true and `mainWindow != null`, execute `await box.ShowWindowDialogAsync(mainWindow)`. Otherwise, execute `await box.ShowAsync()`.
- `ShowCustomAsync`: Accept optional `Window? owner` or default to `GetMainWindow()` if required.
- `ShowYesNoAsync`: Keep modal via `ShowWindowDialogAsync(mainWindow)` when `mainWindow` is available.

## Verification & Testing
1. **Compilation Check**: Verify `dotnet build` succeeds with zero warnings/errors.
2. **Behavior Verification**:
   - Test batch completion with `ModalCompletionMessages = true` (Verify main window input is locked and popup stays on top).
   - Test batch completion with `ModalCompletionMessages = false` (Verify modeless popup behavior).
   - Test error dialogs and info dialogs under both true and false settings.
   - Verify settings persist across application restarts in `%AppData%/Videofy/settings.json`.

# Message Box Theme Colors Design Spec

## Overview
Currently, message box dialogs displayed via `MessageService` use MsBox's default dark styling, resulting in a generic black/dark-gray background that does not seamlessly match Videofy's custom UI color palette (`#1E2228` Secondary Background, `#F8FAFC` Main Text).

This spec outlines centralizing `MessageService` dialog construction to apply Videofy's color theme, proper `CenterOwner` window placement, and theme integration across all message dialog types (`ShowInfoAsync`, `ShowErrorAsync`, `ShowSuccessAsync`, `ShowCustomAsync`, `ShowYesNoAsync`).

## Architecture & Implementation Details

### 1. Centralized Dialog Factory (`MessageService.cs`)
Create a helper method `CreateParams(...)` in `MessageService` to generate standardized `MessageBoxStandardParams` with theme styling:
- **Background Color**: `#1E2228` (Matches `SecondaryBackground` / Settings window)
- **Text / Foreground Color**: `#F8FAFC` (Matches `MainText`)
- **Window Startup Location**: `WindowStartupLocation.CenterOwner`
- **Window Customizer**: Configure window background brush to `#1E2228` and foreground brush to `#F8FAFC`.

```csharp
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
```

### 2. Method Updates
Refactor message creation across `MessageService.cs`:
- `ShowInfoAsync`: Uses `CreateParams(title, message, ButtonEnum.Ok, Icon.Info)`
- `ShowErrorAsync`: Uses `CreateParams(title, message, ButtonEnum.Ok, Icon.Error)`
- `ShowSuccessAsync`: Uses `CreateParams(title, message, ButtonEnum.Ok, Icon.Success)`
- `ShowYesNoAsync`: Uses `CreateParams(title, message, ButtonEnum.YesNo, Icon.Question)`
- `ShowCustomAsync`: Applies default window customizer if not specified in incoming parameters.

### 3. Release Notes Update (`releasenotes.md`)
Add an entry under `## UI Enhancements`:
```markdown
- **Theme-Consistent Message Boxes:** Message box dialogs now follow Videofy's dark theme palette (`#1E2228` secondary background and `#F8FAFC` main text) and automatically center over the active window.
```

## Verification & Testing
1. **Compilation Check**: `dotnet build` succeeds with 0 errors.
2. **Visual Verification**: Message popups open with `#1E2228` background and `#F8FAFC` text matching Videofy's UI theme.

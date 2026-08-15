# Settings Window Card Encapsulation Design Spec

## Overview
Currently, the settings items in `SettingsWindow.axaml` are rendered in open text and checkboxes under section headers, while Hardware Acceleration is placed inside a distinct styled `Border` card box.

This spec details wrapping each logical settings category into an encapsulated visual card container (`Border` with `SecondaryBackground`, `BorderThickness="1"`, `BorderColor`, and `CornerRadius="6"`) to improve UI clarity, accessibility, and consistency across Global Settings.

## Category Boxes Layout

1. **Encoder Settings Box**:
   - Header: `Encoder Settings`
   - Content:
     - Delete original file check & warning text
     - Allow processing already optimized check
     - Bitrate ceiling safety cap check
     - Prevent sleep check
     - Disk space buffer slider
     - Default output format dropdown

2. **Hardware Acceleration Box**:
   - Header: `Hardware Acceleration` + `Auto-detect` button
   - Content: GPU encoder checkboxes list inside card container

3. **Notification & Dialog Settings Box**:
   - Header: `Notification & Dialog Settings`
   - Content:
     - Block app input for Task Completion messages check
     - Block app input for Error & Warning messages check
     - Block app input for Informational messages check

4. **Misc & Advanced Settings Box**:
   - Header: `Misc & Advanced`
   - Content:
     - Auto-check updates check
     - Additional input formats textbox
     - Disable UI Hardware Acceleration check & warning text

## UI Styling Spec (`SettingsWindow.axaml`)
Each category box will follow this uniform XAML pattern:
```xml
<StackPanel Spacing="8">
    <TextBlock Text="[Category Title]" FontSize="16" FontWeight="Bold" Foreground="{DynamicResource SystemAccentColor}"/>
    <Border Background="{DynamicResource SecondaryBackground}"
            BorderBrush="{DynamicResource BorderColor}"
            BorderThickness="1"
            CornerRadius="6"
            Padding="16,14">
        <StackPanel Spacing="12">
            <!-- Category Controls -->
        </StackPanel>
    </Border>
</StackPanel>
```

## Release Notes Update (`releasenotes.md`)
Under `## UI Enhancements`:
```markdown
- **Settings Card Layout Encapsulation:** Encapsulated all setting categories (Encoder Settings, Hardware Acceleration, Notification & Dialog Settings, Misc & Advanced) into distinct visual card containers (`Border` cards with rounded corners and subtle borders) for improved contrast, organization, and visual accessibility.
```

## Verification
1. `dotnet build` succeeds with 0 errors.
2. Verify visual appearance of `SettingsWindow.axaml`.

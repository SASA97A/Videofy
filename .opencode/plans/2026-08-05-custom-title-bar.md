# Custom Title Bar & Window Controls Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the OS native title bar with a custom cross-platform title bar integrated with Videofy's menu bar (`File`, `Edit`, `View`, `Tools`, `Help`) and vector window controls (Minimize, Maximize/Restore, Close) using `FluentIcons.Avalonia` icons.

**Architecture:** Extend client area into decorations (`ExtendClientAreaToDecorationsHint="True"`, `ExtendClientAreaChromeHints="NoChrome"`), add custom 38px title bar grid combining branding, menu bar, drag region, and Fluent vector window action buttons in `MainWindow.axaml` and wire window events in `MainWindow.axaml.cs`.

**Tech Stack:** C# 12, .NET 8, Avalonia UI 11, `FluentIcons.Avalonia` 1.1.258.

## Global Constraints
- Target Framework: .NET 8.0 / Avalonia 11
- Build command: `dotnet build "Video Size Optimizer/Video Size Optimizer.csproj"`
- Code conventions: Standard Videofy MVVM architecture, vector icon rendering, non-blocking window controls.

---

### Task 1: Create Branch & Install `FluentIcons.Avalonia` Package

**Files:**
- Modify: `Video Size Optimizer/Video Size Optimizer.csproj`

- [ ] **Step 1: Create feature branch `feature/custom-title-bar`**

Run: `git checkout -b feature/custom-title-bar`

- [ ] **Step 2: Add `FluentIcons.Avalonia` NuGet package**

Run: `dotnet add "Video Size Optimizer/Video Size Optimizer.csproj" package FluentIcons.Avalonia`

- [ ] **Step 3: Build project to verify package installation**

Run: `dotnet build "Video Size Optimizer/Video Size Optimizer.csproj"`
Expected: Build succeeded with 0 errors.

- [ ] **Step 4: Commit Task 1**

```bash
git add "Video Size Optimizer/Video Size Optimizer.csproj"
git commit -m "feat: add FluentIcons.Avalonia package for cross-platform vector window control icons"
```

---

### Task 2: Implement Code-Behind Window Operations in `MainWindow.axaml.cs`

**Files:**
- Modify: `Video Size Optimizer/Views/MainWindow.axaml.cs`

**Interfaces:**
- Produces: `OnMinimizeClick`, `OnMaximizeRestoreClick`, `OnCloseClick`, `OnTitleBarPointerPressed`

- [ ] **Step 1: Edit `MainWindow.axaml.cs`**

Add event handlers:
```csharp
private void OnMinimizeClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
{
    WindowState = WindowState.Minimized;
}

private void OnMaximizeRestoreClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
{
    WindowState = WindowState == WindowState.Maximized
        ? WindowState.Normal
        : WindowState.Maximized;
}

private void OnCloseClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
{
    Close();
}

private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
{
    if (e.ClickCount == 2)
    {
        OnMaximizeRestoreClick(sender, e);
    }
    else if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
    {
        BeginMoveDrag(e);
    }
}
```

- [ ] **Step 2: Build project**

Run: `dotnet build "Video Size Optimizer/Video Size Optimizer.csproj"`
Expected: Build succeeded with 0 errors.

- [ ] **Step 3: Commit Task 2**

```bash
git add "Video Size Optimizer/Views/MainWindow.axaml.cs"
git commit -m "feat: add window control handlers (minimize, maximize/restore, close, drag) in MainWindow code-behind"
```

---

### Task 3: Build Custom Integrated Title Bar in `MainWindow.axaml`

**Files:**
- Modify: `Video Size Optimizer/Views/MainWindow.axaml`

- [ ] **Step 1: Configure Window properties for client area extension**

In `<Window>` tag:
```xml
ExtendClientAreaToDecorationsHint="True"
ExtendClientAreaChromeHints="NoChrome"
ExtendClientAreaTitleBarHeightHint="38"
```

- [ ] **Step 2: Add `xmlns:ic="using:FluentIcons.Avalonia"` to `<Window>` header**

- [ ] **Step 3: Replace top Menu bar section with Integrated Title Bar Grid**

```xml
<!-- Integrated Custom Title Bar -->
<Border Grid.Row="0"
        Height="38"
        Background="{DynamicResource SecondaryBackground}"
        BorderBrush="{DynamicResource BorderColor}"
        BorderThickness="0,0,0,1">
    <Grid ColumnDefinitions="Auto, Auto, *, Auto">
        
        <!-- Column 0: App Branding -->
        <StackPanel Grid.Column="0" Orientation="Horizontal" Spacing="8" Margin="12,0,10,0" VerticalAlignment="Center">
            <Image Width="16" Height="16" VerticalAlignment="Center">
                <Image.Source>
                    <SvgImage Source="avares://Videofy/Assets/green-folder.svg"/>
                </Image.Source>
            </Image>
            <TextBlock Text="Videofy" FontWeight="Bold" FontSize="12" Foreground="{DynamicResource SystemAccentColor}" VerticalAlignment="Center"/>
        </StackPanel>

        <!-- Column 1: Integrated Main Menu -->
        <Menu Grid.Column="1" Background="Transparent" VerticalAlignment="Center" Margin="0">
            <!-- File, Edit, View, Tools, Help menu items -->
        </Menu>

        <!-- Column 2: Window Drag Region -->
        <Border Grid.Column="2" Background="Transparent" PointerPressed="OnTitleBarPointerPressed">
            <TextBlock Text="Videofy - Video Size Optimizer"
                       FontSize="11"
                       Foreground="{DynamicResource SecondaryText}"
                       HorizontalAlignment="Center"
                       VerticalAlignment="Center"
                       Opacity="0.7"/>
        </Border>

        <!-- Column 3: Window Controls (Minimize, Maximize/Restore, Close) -->
        <StackPanel Grid.Column="3" Orientation="Horizontal" VerticalAlignment="Stretch">
            <Button Width="46"
                    Background="Transparent"
                    BorderThickness="0"
                    Click="OnMinimizeClick"
                    ToolTip.Tip="Minimize"
                    Cursor="Hand">
                <Button.Styles>
                    <Style Selector="Button:pointerover /template/ ContentPresenter#PART_ContentPresenter">
                        <Setter Property="Background" Value="#2BFFFFFF"/>
                    </Style>
                </Button.Styles>
                <ic:SymbolIcon Symbol="Subtract" FontSize="12" Foreground="{DynamicResource MainText}"/>
            </Button>

            <Button Width="46"
                    Background="Transparent"
                    BorderThickness="0"
                    Click="OnMaximizeRestoreClick"
                    ToolTip.Tip="Maximize / Restore"
                    Cursor="Hand">
                <Button.Styles>
                    <Style Selector="Button:pointerover /template/ ContentPresenter#PART_ContentPresenter">
                        <Setter Property="Background" Value="#2BFFFFFF"/>
                    </Style>
                </Button.Styles>
                <Panel>
                    <ic:SymbolIcon Symbol="Maximize" FontSize="12" Foreground="{DynamicResource MainText}" IsVisible="{Binding $parent[Window].WindowState, Converter={x:Static conv:BoolConverters.IsNormalWindowStateConverter}}"/>
                    <ic:SymbolIcon Symbol="SquareMultiple" FontSize="12" Foreground="{DynamicResource MainText}" IsVisible="{Binding $parent[Window].WindowState, Converter={x:Static conv:BoolConverters.IsMaximizedWindowStateConverter}}"/>
                </Panel>
            </Button>

            <Button Width="46"
                    Background="Transparent"
                    BorderThickness="0"
                    Click="OnCloseClick"
                    ToolTip.Tip="Close"
                    Cursor="Hand">
                <Button.Styles>
                    <Style Selector="Button:pointerover /template/ ContentPresenter#PART_ContentPresenter">
                        <Setter Property="Background" Value="#E81123"/>
                        <Setter Property="TextBlock.Foreground" Value="#FFFFFF"/>
                    </Style>
                </Button.Styles>
                <ic:SymbolIcon Symbol="Dismiss" FontSize="12" Foreground="{DynamicResource MainText}"/>
            </Button>
        </StackPanel>
    </Grid>
</Border>
```

- [ ] **Step 4: Build project**

Run: `dotnet build "Video Size Optimizer/Video Size Optimizer.csproj"`
Expected: Build succeeded with 0 errors.

- [ ] **Step 5: Commit Task 3**

```bash
git add "Video Size Optimizer/Views/MainWindow.axaml"
git commit -m "feat: add integrated custom title bar with menu and vector window controls"
```

---

### Task 4: Final Verification & Code Review
1. Run `dotnet build "Video Size Optimizer/Video Size Optimizer.csproj"`.
2. Inspect `git diff` to ensure clean window integration.

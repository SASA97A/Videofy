# Log Window Text Wrapping Fix Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix log message clipping in the log viewer window by wrapping long lines and aligning them past the log entry metadata.

**Architecture:** Update `LogWindow.axaml` item template to use a `DockPanel` (`LastChildFill="True"`) with metadata controls docked to the left and message control wrapping in the remaining space. Set `ScrollViewer.HorizontalScrollBarVisibility="Disabled"` on `LogListBox`.

**Tech Stack:** Avalonia UI, XAML.

## Global Constraints
- Base Branch: `Version-1.4.3`
- Feature Branch: `feature/log-text-wrapping`

---

### Task 1: Update Log Window Layout

**Files:**
- Modify: `Video Size Optimizer/Views/LogWindow.axaml:87-163`

**Interfaces:**
- Consumes: `services:LogEntry`
- Produces: Wrapped log entry view in `LogWindow.axaml`

- [ ] **Step 1: Create feature branch `feature/log-text-wrapping` off `Version-1.4.3`**

Run: `git checkout -b feature/log-text-wrapping Version-1.4.3`

- [ ] **Step 2: Update ListBox and ItemTemplate in `LogWindow.axaml`**

- Add `ScrollViewer.HorizontalScrollBarVisibility="Disabled"` to `LogListBox`.
- Replace horizontal `StackPanel` under `<!-- Normal Log Lines View -->` with `DockPanel`:
```xml
<DockPanel IsVisible="{Binding !IsSectionHeader}"
           LastChildFill="True"
           VerticalAlignment="Center">
    <!-- Timestamp -->
    <TextBlock DockPanel.Dock="Left"
               Text="{Binding FormattedTimestamp}"
               Foreground="#888888"
               Margin="0,0,8,0"
               FontFamily="Consolas, Monospace"
               VerticalAlignment="Center"/>

    <!-- Level Badge / Tag -->
    <Border DockPanel.Dock="Left"
            Classes="level-badge"
            Tag="{Binding Level}"
            CornerRadius="3"
            Padding="5,1"
            Margin="0,0,8,0"
            VerticalAlignment="Center">
        <TextBlock Text="{Binding LevelTag}"
                   FontWeight="Bold"
                   FontSize="11"
                   VerticalAlignment="Center"/>
    </Border>

    <!-- Scope -->
    <TextBlock DockPanel.Dock="Left"
               Text="{Binding Scope, StringFormat='[{0}]'}"
               Foreground="#4DD0E1"
               Margin="0,0,8,0"
               FontWeight="SemiBold"
               VerticalAlignment="Center"/>

    <!-- Message -->
    <TextBlock Text="{Binding Message}"
               Foreground="{DynamicResource MainText}"
               TextWrapping="Wrap"
               VerticalAlignment="Center"/>
</DockPanel>
```

- [ ] **Step 3: Verify build**

Run: `dotnet build "Video Size Optimizer/Video Size Optimizer.csproj"`
Expected: Build succeeded with 0 Errors.

- [ ] **Step 4: Commit**

```bash
git add "Video Size Optimizer/Views/LogWindow.axaml"
git commit -m "fix(log-window): disable horizontal scrolling and use DockPanel to wrap log messages"
```

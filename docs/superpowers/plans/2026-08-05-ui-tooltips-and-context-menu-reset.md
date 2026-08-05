# UI Tooltips & Context Menu Reset Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Create feature branch `feature/ui-tooltips-and-context-menu-reset`, add row context menu reset for completed videos, and audit/add comprehensive hover tooltips and explanations to all UI elements across Videofy.

**Architecture:** 
1. Add `ResetVideoStatusCommand(VideoFile? video)` to `MainWindowViewModel.cs`.
2. Add `Reset Status to Ready` item to DataGrid context menu in `MainWindow.axaml`.
3. Add explanatory `ToolTip.Tip` and `Cursor="Hand"` to all controls, menu items, toolbar buttons, tab headers, and views across the application.

**Tech Stack:** C# 12, .NET 8, Avalonia UI 11, CommunityToolkit.MVVM.

## Global Constraints
- Build command: `dotnet build "Video Size Optimizer/Video Size Optimizer.csproj"`
- Code conventions: Standard Videofy MVVM architecture, CommunityToolkit.Mvvm source generators.

---

### Task 1: Add `ResetVideoStatusCommand` in `MainWindowViewModel.cs`

**Files:**
- Modify: `Video Size Optimizer/ViewModels/MainWindowViewModel.cs`

**Interfaces:**
- Produces: `[RelayCommand] public void ResetVideoStatus(VideoFile? video)`

- [ ] **Step 1: Edit `MainWindowViewModel.cs`**

Add `ResetVideoStatus` method:
```csharp
[RelayCommand]
public void ResetVideoStatus(VideoFile? video)
{
    var targets = new List<VideoFile>();
    if (video != null)
    {
        targets.Add(video);
    }
    else
    {
        targets = Videos.Where(v => v.IsSelected).ToList();
        if (targets.Count == 0 && Videos.Count > 0)
        {
            targets = Videos.ToList();
        }
    }

    foreach (var v in targets)
    {
        v.IsCompleted = false;
        v.IsProcessing = false;
        v.Progress = 0;

        var index = DisplayedVideos.IndexOf(v);
        if (index != -1)
        {
            DisplayedVideos[index] = v;
        }
    }

    LogService.Instance.Log($"Reset status for {targets.Count} video(s) to Ready.", LogLevel.Info, "Main");
}
```

- [ ] **Step 2: Build project**

Run: `dotnet build "Video Size Optimizer/Video Size Optimizer.csproj"`
Expected: Build succeeded with 0 errors.

- [ ] **Step 3: Commit Task 1**

```bash
git add "Video Size Optimizer/ViewModels/MainWindowViewModel.cs"
git commit -m "feat: add ResetVideoStatus command supporting single row and batch reset"
```

---

### Task 2: DataGrid Context Menu & Main Window Tooltips (`MainWindow.axaml`)

**Files:**
- Modify: `Video Size Optimizer/Views/MainWindow.axaml`

- [ ] **Step 1: Edit `MainWindow.axaml`**

1. Update DataGrid Row ContextMenu:
```xml
<ContextMenu>
    <MenuItem Header="Reset Status to Ready"
              Command="{Binding $parent[DataGrid].DataContext.ResetVideoStatusCommand}"
              CommandParameter="{Binding}"
              ToolTip.Tip="Reset this video's completed status back to Ready so it can be re-processed" />
    <Separator />
    <MenuItem Header="Open Folder in Explorer"
              Command="{Binding $parent[DataGrid].DataContext.OpenFileFolderCommand}"
              CommandParameter="{Binding}"
              ToolTip.Tip="Open containing folder in File Explorer and select file" />
</ContextMenu>
```

2. Add comprehensive tooltips to all Menu items (File, Edit, View, Tools, Help), top bar buttons (Open Folder, Toggle All, Refresh List, Reset Status, View Logs), search bar, column headers, override button, remove button, and TabControl headers.

- [ ] **Step 2: Build project**

Run: `dotnet build "Video Size Optimizer/Video Size Optimizer.csproj"`
Expected: Build succeeded with 0 errors.

- [ ] **Step 3: Commit Task 2**

```bash
git add "Video Size Optimizer/Views/MainWindow.axaml"
git commit -m "feat: add row context menu reset and main window tooltips"
```

---

### Task 3: Audit & Add Tooltips across View Tabs (`CompressionView`, `ConversionView`, `SplitView`, `MergeView`)

**Files:**
- Modify: `Video Size Optimizer/Views/CompressionView.axaml`
- Modify: `Video Size Optimizer/Views/ConversionView.axaml`
- Modify: `Video Size Optimizer/Views/SplitView.axaml`
- Modify: `Video Size Optimizer/Views/MergeView.axaml`

- [ ] **Step 1: Add ToolTips to `CompressionView.axaml`**
- [ ] **Step 2: Add ToolTips to `ConversionView.axaml`**
- [ ] **Step 3: Add ToolTips to `SplitView.axaml`**
- [ ] **Step 4: Add ToolTips to `MergeView.axaml`**

- [ ] **Step 5: Build project**

Run: `dotnet build "Video Size Optimizer/Video Size Optimizer.csproj"`
Expected: Build succeeded with 0 errors.

- [ ] **Step 6: Commit Task 3**

```bash
git add "Video Size Optimizer/Views/*.axaml"
git commit -m "feat: add comprehensive tooltips to all view controls across tabs"
```

---

### Task 4: Audit & Add Tooltips across Dialog Windows (`SettingsWindow`, `RenameWindow`, `MergeGroupsWindow`)

**Files:**
- Modify: `Video Size Optimizer/Views/SettingsWindow.axaml`
- Modify: `Video Size Optimizer/Views/RenameWindow.axaml`
- Modify: `Video Size Optimizer/Views/MergeGroupsWindow.axaml`

- [ ] **Step 1: Add ToolTips to dialog windows**

- [ ] **Step 2: Build project**

Run: `dotnet build "Video Size Optimizer/Video Size Optimizer.csproj"`
Expected: Build succeeded with 0 errors.

- [ ] **Step 3: Commit Task 4**

```bash
git add "Video Size Optimizer/Views/SettingsWindow.axaml" "Video Size Optimizer/Views/RenameWindow.axaml" "Video Size Optimizer/Views/MergeGroupsWindow.axaml"
git commit -m "feat: add tooltips and explanations to dialog windows"
```

---

### Task 5: Final Verification & Build Check
1. Run `dotnet build "Video Size Optimizer/Video Size Optimizer.csproj"`.
2. Inspect `git diff` and git log.

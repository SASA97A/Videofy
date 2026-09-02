# Video Merge Grouping & Interface Fixes Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement context-menu button enabling, grid "Group Assign" column swap, and unified grouping property updates.

**Architecture:** Change `AssignToGroup` command parameter to `object`, add `GroupIndexUi` to `VideoFile` with property changed listener, add `GroupIndexChoices` (up to Group 30) to VM, and bind columns in `MainWindow.axaml` using `IsMergeTabActive`.

**Tech Stack:** C# .NET 9, Avalonia UI, XAML.

## Global Constraints
- Branch: `feature/smart-video-merge`

---

### Task 1: Implement Grouping Pipeline & Grid Column Customization

**Files:**
- Modify: `Video Size Optimizer/Models/VideoFile.cs:22-30`
- Modify: `Video Size Optimizer/ViewModels/MainWindowViewModel.cs:30-100,1350-1420`
- Modify: `Video Size Optimizer/Views/MainWindow.axaml:450-460,524-557`

**Interfaces:**
- Consumes: DataGrid bindings
- Produces: `MainWindowViewModel.SelectedVideo`, `MainWindowViewModel.MergeMenuHeader`, `MainWindowViewModel.MergeOutputFormats`, correct grouping target scoping.

- [ ] **Step 1: Update `VideoFile.cs` model**

Add `GroupIndexUi` property:
```csharp
[ObservableProperty]
[NotifyPropertyChangedFor(nameof(HasGroup))]
[NotifyPropertyChangedFor(nameof(GroupOrderDisplay))]
private int _groupIndexUi = 0;
```

- [ ] **Step 2: Update `MainWindowViewModel.cs` grouping and UI bindings**

- Add `GroupIndexChoices` in constructor (pre-populating "None", "Group 1", ..., "Group 30").
- Modify `AssignToGroup(object? groupIdObj)` to parse `int` parameter to avoid disabled menu items.
- Update `VideoFile_PropertyChanged` to handle `GroupIndexUi` changes, normalizations, and group refreshes.
- Simplify `AddToNewGroup`, `AssignToGroup`, and `RemoveFromGroup` to set `GroupIndexUi` on targets.

- [ ] **Step 3: Update `MainWindow.axaml` columns**

- Set `IsVisible="{Binding !$parent[DataGrid].DataContext.IsMergeTabActive}"` on the "Override" column.
- Add a new "Group Assign" ComboBox column, visible when `IsMergeTabActive` is true, bound to `GroupIndexUi`.

- [ ] **Step 4: Verify build**

Run: `dotnet build "Video Size Optimizer/Video Size Optimizer.csproj"`
Expected: Build succeeded with 0 Errors.

- [ ] **Step 5: Commit**

```bash
git add "Video Size Optimizer/Models/VideoFile.cs" "Video Size Optimizer/ViewModels/MainWindowViewModel.cs" "Video Size Optimizer/Views/MainWindow.axaml"
git commit -m "fix(merge): fix disabled assign button, add group assign column, and unify grouping logic"
```

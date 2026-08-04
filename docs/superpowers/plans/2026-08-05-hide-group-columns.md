# Hide Group Columns Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Hide the "Group Assign" and "Group" columns, and dynamically show the "Override" column only in the Optimize and Convert tabs.

**Architecture:** Update the view model to expose an `IsOverrideVisible` property, notify when the tab changes, and bind the DataGrid column visibilities directly in XAML.

**Tech Stack:** C#, Avalonia UI (v11.3.10), .NET 8.0

## Global Constraints
- Target Framework: net8.0
- Avalonia version: 11.3.10
- Branch from Version-1.4.3 (Already done)

---

### Task 1: Update MainWindowViewModel to manage column visibility

**Files:**
- Modify: `Video Size Optimizer/ViewModels/MainWindowViewModel.cs:64-65`
- Modify: `Video Size Optimizer/ViewModels/MainWindowViewModel.cs:1388-1390`

**Interfaces:**
- Produces: `IsOverrideVisible` boolean property, notifying on selected tab changes.

- [ ] **Step 1: Read ViewModel file to locate tab change handlers and property declarations**
  Read `Video Size Optimizer/ViewModels/MainWindowViewModel.cs` around lines 55-75 and 1380-1400.

- [ ] **Step 2: Add IsOverrideVisible property to MainWindowViewModel**
  Add the following property:
  ```csharp
  public bool IsOverrideVisible => SelectedTabIndex == 0 || SelectedTabIndex == 1;
  ```

- [ ] **Step 3: Update OnSelectedTabIndexChanged to notify IsOverrideVisible**
  Modify:
  ```csharp
  partial void OnSelectedTabIndexChanged(int value)
  {
      OnPropertyChanged(nameof(IsMergeTabActive));
      OnPropertyChanged(nameof(IsOverrideVisible));
  }
  ```

- [ ] **Step 4: Verify project compilation**
  Run: `dotnet build "Video Size Optimizer/Video Size Optimizer.csproj" -c Debug`
  Expected: Compile successfully.

- [ ] **Step 5: Commit changes**
  Run: `git commit -am "feat: add IsOverrideVisible property and notify on SelectedTabIndex change"`

---

### Task 2: Bind Column Visibility in MainWindow.axaml

**Files:**
- Modify: `Video Size Optimizer/Views/MainWindow.axaml`

- [ ] **Step 1: Read MainWindow.axaml around lines 450-480 and 535-545**
  Verify the exact positions of the `Group Assign`, `Group`, and `Override` DataGridTemplateColumn elements.

- [ ] **Step 2: Update Group Assign Column visibility binding**
  Change the binding:
  ```xaml
  <DataGridTemplateColumn Header="Group Assign" Width="140" IsVisible="{Binding IsMergeTabActive}">
  ```

- [ ] **Step 3: Update Group Column visibility binding**
  Change the binding:
  ```xaml
  <DataGridTemplateColumn Header="Group" Width="100" CanUserResize="False" IsVisible="{Binding IsMergeTabActive}">
  ```

- [ ] **Step 4: Update Override Column visibility binding**
  Change the binding:
  ```xaml
  <DataGridTemplateColumn Header="Override" Width="110" IsVisible="{Binding IsOverrideVisible}">
  ```

- [ ] **Step 5: Verify project compilation**
  Run: `dotnet build "Video Size Optimizer/Video Size Optimizer.csproj" -c Debug`
  Expected: Compile successfully without any XAML validation warnings or errors.

- [ ] **Step 6: Commit changes**
  Run: `git commit -am "feat: bind Group, Group Assign, and Override columns visibility to ViewModel properties"`

# Spec: Hide/Disable Group and Group Assign Columns in Non-Merge Tabs

## Overview
Enhance the Videofy UI to hide the "Group Assign" and "Group" columns, and show/hide the "Override" settings column dynamically depending on the selected tab in the main interface.

## Goals
1. Hide the "Group Assign" and "Group" columns in all tabs except the "MERGE (SMART BATCH)" tab.
2. Hide the "Override" column in all tabs except the "OPTIMIZE (COMPRESS)" and "CONVERT (INSTANT)" tabs.

## Implementation Details

### View Model (`MainWindowViewModel.cs`)
- Define `IsOverrideVisible` property:
  ```csharp
  public bool IsOverrideVisible => SelectedTabIndex == 0 || SelectedTabIndex == 1;
  ```
- Update `OnSelectedTabIndexChanged(int value)` to notify changes on both `IsMergeTabActive` and `IsOverrideVisible`:
  ```csharp
  partial void OnSelectedTabIndexChanged(int value)
  {
      OnPropertyChanged(nameof(IsMergeTabActive));
      OnPropertyChanged(nameof(IsOverrideVisible));
  }
  ```

### View (`MainWindow.axaml`)
- Update the DataGrid columns' visibility bindings to bind directly to the inherited `DataContext` properties of `MainWindowViewModel`:
  - **Group Assign Column**:
    ```xaml
    IsVisible="{Binding IsMergeTabActive}"
    ```
  - **Group Column**:
    ```xaml
    IsVisible="{Binding IsMergeTabActive}"
    ```
  - **Override Column**:
    ```xaml
    IsVisible="{Binding IsOverrideVisible}"
    ```

## Verification Plan
1. Compile the application.
2. Run the application and switch tabs to verify that:
   - "Group Assign" and "Group" columns are only visible when the "MERGE" tab is active.
   - "Override" column is only visible when the "OPTIMIZE" or "CONVERT" tabs are active.

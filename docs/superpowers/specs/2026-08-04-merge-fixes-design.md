# Spec: Video Merge Context Menu & Target Format Fixes

## 1. Overview
This specification details the improvements to context-menu behavior for video merging and dynamic grouping to support both batch checked-item assignments and individual clicked-row assignments. The context menu header text dynamically updates based on user selection state. Additionally, it filters out the `"Original"` format choice in the MERGE tab.

## 2. Requirements & Constraints
- **Fix Group Appending (Enabled MenuItem):** Change the parameter type of `AssignToGroupCommand` from `int` to `object?` to prevent initialization null-checks from disabling the MenuItem.
- **Group Assign Grid Column:**
  - Add a "Group Assign" ComboBox column to the DataGrid, visible *only* when the Merge tab is active.
  - Hide the "Override" column when the Merge tab is active.
  - The ComboBox displays a list of choices: `None, Group 1, Group 2, ..., Group 30`.
  - Binding the ComboBox's `SelectedIndex` directly to `VideoFile.GroupIndexUi` (two-way binding).
- **Unified Group Assignment Pipeline:**
  - Add `GroupIndexUi` to `VideoFile`.
  - In `VideoFile_PropertyChanged`, listen to `GroupIndexUi`. When changed:
    - Update `GroupNumber` (set to `null` if index is `0`, otherwise set to index).
    - If assigned to a group, set `SequenceNumber` to the next sequence.
    - Normalize sequence numbers in both the previous group and the new group.
    - Refresh existing group options list.
  - Update `AddToNewGroup`, `AssignToGroup`, and `RemoveFromGroup` commands to simply set `GroupIndexUi` on targets, ensuring unified sequence and state management.
- **Remove "Original" Format from Merge Options:** The target formats in the MERGE tab must exclude the `"Original"` choice to prevent format ambiguity when combining mixed containers.

## 3. UI Changes & Bindings

### MainWindow Context Menu (`MainWindow.axaml`)
Bind the `Header` of the MenuItem to the dynamic property `MergeMenuHeader`:
```xml
<MenuItem Header="{Binding $parent[DataGrid].DataContext.MergeMenuHeader}" 
          IsVisible="{Binding $parent[DataGrid].DataContext.IsMergeTabActive}">
    <MenuItem Header="Add to New Group" Command="{Binding $parent[DataGrid].DataContext.AddToNewGroupCommand}"/>
    <MenuItem Header="Add to Existing Group" IsEnabled="{Binding $parent[DataGrid].DataContext.HasExistingGroups}" ItemsSource="{Binding $parent[DataGrid].DataContext.ExistingGroups}">
        <MenuItem.ItemTemplate>
            <DataTemplate x:DataType="vm:GroupOption">
                <MenuItem Header="{Binding Name}" Command="{Binding $parent[DataGrid].DataContext.AssignToGroupCommand}" CommandParameter="{Binding Id}"/>
            </DataTemplate>
        </MenuItem.ItemTemplate>
    </MenuItem>
    <Separator/>
    <MenuItem Header="Remove from Group" Command="{Binding $parent[DataGrid].DataContext.RemoveFromGroupCommand}"/>
</MenuItem>
```

### MainWindow DataGrid (`MainWindow.axaml`)
Bind the SelectedItem of the DataGrid to `SelectedVideo`:
```xml
<DataGrid ItemsSource="{Binding DisplayedVideos}"
          SelectedItem="{Binding SelectedVideo}"
          IsReadOnly="{Binding IsBusy}"
          ...
```

### Merge View (`MergeView.axaml`)
Bind the combobox to `MergeOutputFormats`:
```xml
<ComboBox ItemsSource="{Binding MergeOutputFormats}"
          SelectedItem="{Binding MergeTargetFormat}"
          ...
```

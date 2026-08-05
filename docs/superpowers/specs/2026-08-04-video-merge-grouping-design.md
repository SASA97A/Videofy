# Spec: Smart Video Merge with Dynamic Grouping & Group Manager Window

## 1. Overview
This feature introduces a comprehensive sequential video merger tab ("MERGE") to Videofy v1.4.3. Users can group selected videos into arbitrary merge groups (Group 1, Group 2, etc.) and define their rendering sequence using context-menu commands (visible only while the `MERGE` tab is selected) or via a visual **Group Manager Window** (`MergeGroupsWindow`). During processing, Videofy validates that each group contains at least 2 videos and merges each group into an output file (e.g. `merged_video_Group1.mp4`).

If all videos in a group share identical video/audio parameters, Videofy performs an instant, 1:1 lossless concat remux (`-c copy`). If there are differences or if forced by the user, Videofy scales and pads all streams to a master canvas (matching maximum dimensions across inputs) to prevent aspect ratio distortion or stretching, using hardware/GPU acceleration when available.

## 2. Requirements & Constraints
- **Multi-Group Batch Merging:** Supports merging multiple groups of videos in a single batch operation.
- **Minimum Requirement:** Each group must contain at least 2 videos. Single-video groups are skipped with a warning.
- **Context-Menu Integration (Conditional):** Context-menu items for group management are only visible when `SelectedTabIndex == 3` (`MERGE` tab).
  - "Add to Merge Group" -> "Add to New Group"
  - "Add to Merge Group" -> "Add to Existing Group" -> (Dynamic list of existing active groups)
  - "Remove from Group"
  - "Show Group Manager..."
- **Group Manager Window (`MergeGroupsWindow`):**
  - Left panel: Lists active groups.
  - Right panel: Displays videos in the selected group with controls to reorder sequence (`▲` / `▼`) or remove (`❌`).
- **DataGrid Badge:** A badge displaying `G{Group} - #{Order}` is rendered in the `DataGrid` for grouped videos.
- **Merge Engine (`FfmpegService`):**
  - Reads detailed `ffprobe` stream metadata for all videos in a group.
  - Performs lossless Concat Demuxer copy if streams are identical.
  - Builds dynamic scale/pad filtergraph and audio normalizer if streams differ or re-encoding is forced.
  - Generates `ffmetadata.txt` chapter markers for merged segments.

## 3. Data Model Extensions (`VideoFile.cs` & `MainWindowViewModel.cs`)

### `VideoFile.cs`
```csharp
[ObservableProperty]
[NotifyPropertyChangedFor(nameof(HasGroup))]
[NotifyPropertyChangedFor(nameof(GroupOrderDisplay))]
private int? _groupNumber = null;

[ObservableProperty]
[NotifyPropertyChangedFor(nameof(GroupOrderDisplay))]
private int _sequenceNumber = 1;

public bool HasGroup => GroupNumber.HasValue;
public string GroupOrderDisplay => HasGroup ? $"G{GroupNumber} - #{SequenceNumber}" : string.Empty;
```

### `GroupOption` Helper (`MainWindowViewModel.cs`)
```csharp
public partial class GroupOption : ObservableObject
{
    public int Id { get; set; }
    public string Name => $"Group {Id}";
}
```

### `MainWindowViewModel.cs` Properties & Commands
- `IsMergeTabActive` => `SelectedTabIndex == 3`
- `ExistingGroups`: `ObservableCollection<GroupOption>` dynamically updated whenever grouping changes.
- Commands: `AddToNewGroupCommand`, `AssignToGroupCommand`, `RemoveFromGroupCommand`, `OpenGroupManagerCommand`.

## 4. UI Specifications

### `MainWindow.axaml`
- Context-menu items bound to `IsMergeTabActive`.
- DataGrid column `Merge Group` showing badge when `HasGroup` is true.

### `MergeView.axaml`
UserControl containing:
- Left card: Target format combo box, Force Re-encode checkbox, and instructions.
- Right card: Start / Stop action buttons and Pause button.

### `MergeGroupsWindow.axaml`
Window dialog for viewing and reordering merge groups.

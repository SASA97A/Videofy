# Release Notes - Videofy v1.4.3

## New Features
- **GPU Hardware Auto-Detection:** Videofy now automatically scans and detects compatible GPU hardware encoders on your system during application startup and within the settings menu.
- **Cross-Platform Support:** Compatible with NVIDIA NVENC, AMD AMF, and Intel QSV across Windows, macOS, and Linux.
- **Improved Settings UX:** Unsupported hardware options are now disabled in the Settings UI until they are detected. Added an **Auto-detect** button to settings to manually re-scan hardware at any time.
- **Smart Video Merge Tab:** Introduced batch merge capabilities allowing sequential concatenation of multiple groups of videos. Supports dynamic group assignments and sequence ordering directly from the grid context menu or the dedicated Group Manager Window.
- **Group Assign ComboBox:** Integrates a context-aware dropdown inside the main grid to assign groups directly to rows when the `MERGE` tab is active, replacing the compression override settings column.
- **Merge Tab Encoder Selection:** The Merge tab now includes its own dedicated encoder dropdown, allowing users to select GPU-accelerated encoders (NVIDIA NVENC, AMD AMF, Intel QSV) or CPU encoding independently from the compression tab. This removes the CPU-only limitation when re-encoding merged videos.

## UI Enhancements
- **Dynamic Column Visibility:** The "Group Assign" and "Group" columns are now only visible when the MERGE tab is active. The "Override" settings column is shown only in the OPTIMIZE and CONVERT tabs. This keeps the grid clean and contextually relevant to the selected workflow.

## Fixes & Improvements
- **Log Viewer Wrapping Fix:** Resolved an issue where long log lines were clipped horizontally when the log window was resized. Long log entries now wrap properly while maintaining vertical alignment with the start of the message text.
- **UI Label Fix:** Fixed the button label in the Settings Window from "Save & Close" to "Close".
- **Dynamic Grouping Context Menu:** Appending more files to existing groups is fully enabled, with context menu options updating dynamically based on checkbox selections.
- **BOM-less FFmpeg I/O:** Temp metadata and concat list files are written in UTF-8 without BOM, resolving the immediate FFmpeg crash `-541478725` (`AVERROR_INVALIDDATA`).
- **Consistent FFmpeg Codec Flag:** Normalized video codec flag from `-vcodec` to `-c:v` across all encoder paths for consistency with FFmpeg's modern syntax.

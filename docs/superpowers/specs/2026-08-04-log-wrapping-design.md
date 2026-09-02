# Spec: Log Window Text Wrapping & Layout Fix

## 1. Overview
This specification details the fix for text clipping in the Log Window (`LogWindow.axaml`). Currently, long log messages overflow horizontally without wrapping when the window is resized to a smaller width. This fix ensures log messages wrap to subsequent lines while maintaining vertical alignment with the start of the message text (indented past the timestamp, level badge, and scope).

## 2. Requirements & Constraints
- **Word Wrapping:** Long log messages must automatically wrap to the next line.
- **Vertical Indentation Alignment:** Wrapped text lines must align vertically with the start of the log message text (indented past the timestamp, level badge, and scope).
- **Disabled Horizontal Scrolling:** Horizontal scrollbar visibility must be set to `Disabled` on `LogListBox` so content stays constrained within the window viewport.

## 3. UI Layout Changes (`LogWindow.axaml`)
1. On `LogListBox`, add `ScrollViewer.HorizontalScrollBarVisibility="Disabled"`.
2. Replace the horizontal `StackPanel` in the item template for normal log lines with a `DockPanel` (`LastChildFill="True"`):
   - Timestamp `TextBlock`: `DockPanel.Dock="Left"`
   - Level Badge `Border`: `DockPanel.Dock="Left"`
   - Scope `TextBlock`: `DockPanel.Dock="Left"`
   - Message `TextBlock`: Last child of `DockPanel`, `TextWrapping="Wrap"`

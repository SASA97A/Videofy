# Merge Feature Branch to Release & Update Release Notes Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Merge `feature/smart-video-merge` into `Version-1.4.3` and update `releasenotes.md`.

**Architecture:** Switch branches, run `git merge feature/smart-video-merge`, update `releasenotes.md`, verify build, and commit.

**Tech Stack:** Git, Markdown.

## Global Constraints
- Target Branch: `Version-1.4.3`
- Source Branch: `feature/smart-video-merge`
- Output File: `releasenotes.md`

---

### Task 1: Merge Branch & Update Release Notes

**Files:**
- Modify: `releasenotes.md`

- [ ] **Step 1: Switch to `Version-1.4.3` branch**

Run: `git checkout Version-1.4.3`

- [ ] **Step 2: Merge `feature/smart-video-merge`**

Run: `git merge feature/smart-video-merge`

- [ ] **Step 3: Update `releasenotes.md` file**

Write the updated release notes:
```markdown
# Release Notes - Videofy v1.4.3

## New Features
- **GPU Hardware Auto-Detection:** Videofy now automatically scans and detects compatible GPU hardware encoders on your system during application startup and within the settings menu.
- **Cross-Platform Support:** Compatible with NVIDIA NVENC, AMD AMF, and Intel QSV across Windows, macOS, and Linux.
- **Improved Settings UX:** Unsupported hardware options are now disabled in the Settings UI until they are detected. Added an **Auto-detect** button to settings to manually re-scan hardware at any time.
- **Smart Video Merge Tab:** Introduced batch merge capabilities allowing sequential concatenation of multiple groups of videos. Supports dynamic group assignments and sequence ordering directly from the grid context menu or the dedicated Group Manager Window.
- **Group Assign ComboBox:** Integrates a context-aware dropdown inside the main grid to assign groups directly to rows when the `MERGE` tab is active, replacing the compression override settings column.

## Fixes & Improvements
- **Log Viewer Wrapping Fix:** Resolved an issue where long log lines were clipped horizontally when the log window was resized. Long log entries now wrap properly while maintaining vertical alignment with the start of the message text.
- **UI Label Fix:** Fixed the button label in the Settings Window from "Save & Close" to "Close".
- **Dynamic Grouping Context Menu:** Appending more files to existing groups is fully enabled, with context menu options updating dynamically based on checkbox selections.
- **BOM-less FFmpeg I/O:** Temp metadata and concat list files are written in UTF-8 without BOM, resolving the immediate FFmpeg crash `-541478725` (`AVERROR_INVALIDDATA`).
```

- [ ] **Step 4: Verify build**

Run: `dotnet build "Video Size Optimizer/Video Size Optimizer.csproj"`
Expected: Build succeeded with 0 Errors.

- [ ] **Step 5: Commit release notes**

```bash
git add releasenotes.md
git commit -m "docs: update release notes for v1.4.3 with video merge and BOM fixes"
```

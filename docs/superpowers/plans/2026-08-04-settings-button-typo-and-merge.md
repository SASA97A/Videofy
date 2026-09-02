# Settings Button Typo Fix & Merge Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix settings button text, merge `feature/log-text-wrapping` into `Version-1.4.3`, and update `releasenotes.md`.

**Architecture:** Change button text in `SettingsWindow.axaml`, commit to feature branch, merge into `Version-1.4.3`, and update `releasenotes.md`.

**Tech Stack:** Avalonia UI XAML, Git, Markdown.

## Global Constraints
- Target Branch: `Version-1.4.3`
- Source Branch: `feature/log-text-wrapping`
- Output File: `releasenotes.md`

---

### Task 1: Fix Settings Button Typo, Merge Branch, and Update Release Notes

**Files:**
- Modify: `Video Size Optimizer/Views/SettingsWindow.axaml:172`
- Modify: `releasenotes.md`

- [ ] **Step 1: Fix button text in `SettingsWindow.axaml`**

Change `Content="Save &amp; Close"` to `Content="Close"` at line 172 in `SettingsWindow.axaml`.

- [ ] **Step 2: Commit fix on feature branch**

```bash
git add "Video Size Optimizer/Views/SettingsWindow.axaml"
git commit -m "fix(settings): change Save & Close button label to Close"
```

- [ ] **Step 3: Checkout `Version-1.4.3` and merge `feature/log-text-wrapping`**

```bash
git checkout Version-1.4.3
git merge feature/log-text-wrapping
```

- [ ] **Step 4: Update `releasenotes.md`**

Update `releasenotes.md` content:
```markdown
# Release Notes - Videofy v1.4.3

## New Features
- **GPU Hardware Auto-Detection:** Videofy now automatically scans and detects compatible GPU hardware encoders on your system during application startup and within the settings menu.
- **Cross-Platform Support:** Compatible with NVIDIA NVENC, AMD AMF, and Intel QSV across Windows, macOS, and Linux.
- **Improved Settings UX:** Unsupported hardware options are now disabled in the Settings UI until they are detected. Added an **Auto-detect** button to settings to manually re-scan hardware at any time.

## Fixes & Improvements
- **Log Viewer Wrapping Fix:** Resolved an issue where long log lines were clipped horizontally when the log window was resized. Long log entries now wrap properly while maintaining vertical alignment with the start of the message text.
- **UI Label Fix:** Fixed the button label in the Settings Window from "Save & Close" to "Close".
```

- [ ] **Step 5: Verify build**

Run: `dotnet build "Video Size Optimizer/Video Size Optimizer.csproj"`
Expected: Build succeeded with 0 Errors.

- [ ] **Step 6: Commit `releasenotes.md`**

```bash
git add releasenotes.md
git commit -m "docs: update release notes for v1.4.3 with log wrapping and button fix"
```

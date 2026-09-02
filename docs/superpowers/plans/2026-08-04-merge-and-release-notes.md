# Merge Feature Branch & Create Release Notes Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Merge `feature/hardware-detection` into `Version-1.4.3` and create `releasenotes.md`.

**Architecture:** Switch branches, run `git merge feature/hardware-detection`, create `releasenotes.md`, verify build, and commit.

**Tech Stack:** Git, Markdown.

## Global Constraints
- Target Branch: `Version-1.4.3`
- Source Branch: `feature/hardware-detection`
- Output File: `releasenotes.md`

---

### Task 1: Merge Branch & Add Release Notes File

**Files:**
- Create: `releasenotes.md`

**Interfaces:**
- Consumes: `feature/hardware-detection` git commits
- Produces: `releasenotes.md` on `Version-1.4.3`

- [ ] **Step 1: Switch to `Version-1.4.3` branch**

Run: `git checkout Version-1.4.3`

- [ ] **Step 2: Merge `feature/hardware-detection`**

Run: `git merge feature/hardware-detection`

- [ ] **Step 3: Create `releasenotes.md` file**

Write the following content to `releasenotes.md`:
```markdown
# Release Notes - Videofy v1.4.3

## New Features
- **GPU Hardware Auto-Detection:** Videofy now automatically scans and detects compatible GPU hardware encoders on your system during application startup and within the settings menu.
- **Cross-Platform Support:** Compatible with NVIDIA NVENC, AMD AMF, and Intel QSV across Windows, macOS, and Linux.
- **Improved Settings UX:** Unsupported hardware options are now disabled in the Settings UI until they are detected. Added an **Auto-detect** button to settings to manually re-scan hardware at any time.
```

- [ ] **Step 4: Verify build**

Run: `dotnet build "Video Size Optimizer/Video Size Optimizer.csproj"`
Expected: Build succeeded with 0 Errors.

- [ ] **Step 5: Commit release notes**

```bash
git add releasenotes.md
git commit -m "docs: add release notes for v1.4.3"
```

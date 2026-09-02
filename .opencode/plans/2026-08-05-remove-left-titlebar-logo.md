# Title Bar Branding Cleanup Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove the redundant left-side logo and "Videofy" text from the custom title bar, keeping only the centered title ("Videofy - Video Size Optimizer") and moving the Main Menu bar directly to the top-left corner.

**Architecture:** Update `Grid.ColumnDefinitions` to `Auto, *, Auto` in `MainWindow.axaml` and remove Column 0 branding `StackPanel`.

**Tech Stack:** C# 12, .NET 8, Avalonia UI 11, `FluentIcons.Avalonia`.

## Global Constraints
- Target Framework: .NET 8.0 / Avalonia 11
- Build command: `dotnet build "Video Size Optimizer/Video Size Optimizer.csproj"`
- Code conventions: Standard Videofy MVVM architecture, vector icon rendering.

---

### Task 1: Update Integrated Title Bar Layout in `MainWindow.axaml`

**Files:**
- Modify: `Video Size Optimizer/Views/MainWindow.axaml`

- [ ] **Step 1: Remove Left Branding StackPanel and adjust Grid Columns**

In `MainWindow.axaml`:
1. Change `<Grid ColumnDefinitions="Auto, Auto, *, Auto">` to `<Grid ColumnDefinitions="Auto, *, Auto">`.
2. Remove `<StackPanel Grid.Column="0">` containing logo image and "Videofy" TextBlock.
3. Update `Menu` to `Grid.Column="0"`.
4. Update Drag Handle Border to `Grid.Column="1"`.
5. Update Window Control buttons StackPanel to `Grid.Column="2"`.

- [ ] **Step 2: Build project**

Run: `dotnet build "Video Size Optimizer/Video Size Optimizer.csproj"`
Expected: Build succeeded with 0 errors.

- [ ] **Step 3: Commit Task 1**

```bash
git add "Video Size Optimizer/Views/MainWindow.axaml"
git commit -m "feat: remove left titlebar logo and branding text, keeping centered title"
```

---

### Task 2: Final Verification & Code Review
1. Run `dotnet build "Video Size Optimizer/Video Size Optimizer.csproj"`.
2. Inspect `git diff` to ensure clean title bar layout.

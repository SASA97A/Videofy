# Spec: Merge Smart Video Merge Feature to Version-1.4.3 & Update Release Notes

## 1. Overview
This specification details merging the completed `feature/smart-video-merge` branch into `Version-1.4.3` and updating `releasenotes.md` with all v1.4.3 features and bug fixes.

## 2. Requirements & Steps
1. **Switch Branch:** Checkout `Version-1.4.3`.
2. **Merge Feature Branch:** Run `git merge feature/smart-video-merge`.
3. **Update Release Notes:** Write all v1.4.3 additions (GPU Hardware Auto-Detection, Log Viewer Text Wrapping, Smart Video Merge with Dynamic Grouping & Group Manager, BOM-less FFmpeg I/O, and UI button fixes) to `releasenotes.md`.
4. **Verification:** Verify `dotnet build` succeeds.
5. **Commit:** Commit `releasenotes.md` to `Version-1.4.3`.

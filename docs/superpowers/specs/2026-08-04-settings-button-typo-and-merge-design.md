# Spec: Settings Button Typo Fix & Feature Branch Merge to Version-1.4.3

## 1. Overview
This specification covers fixing the button text in `SettingsWindow.axaml` (changing `"Save & Close"` to `"Close"`), merging the `feature/log-text-wrapping` branch into `Version-1.4.3`, and updating `releasenotes.md` with all new fixes.

## 2. Requirements
1. **Settings Window Button:** Update `Content="Save &amp; Close"` to `Content="Close"` in `SettingsWindow.axaml`.
2. **Branch Merge:** Merge `feature/log-text-wrapping` into `Version-1.4.3` locally.
3. **Release Notes Update:** Update `releasenotes.md` to document the log viewer wrapping fix and settings button label update.
4. **Verification & Commit:** Ensure `dotnet build` succeeds and commit all changes to `Version-1.4.3`.

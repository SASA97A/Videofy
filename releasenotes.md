# Release Notes - Videofy v1.4.3

## New Features
- **GPU Hardware Auto-Detection:** Videofy now automatically scans and detects compatible GPU hardware encoders on your system during application startup and within the settings menu.
- **Cross-Platform Support:** Compatible with NVIDIA NVENC, AMD AMF, and Intel QSV across Windows, macOS, and Linux.
- **Improved Settings UX:** Unsupported hardware options are now disabled in the Settings UI until they are detected. Added an **Auto-detect** button to settings to manually re-scan hardware at any time.

## Fixes & Improvements
- **Log Viewer Wrapping Fix:** Resolved an issue where long log lines were clipped horizontally when the log window was resized. Long log entries now wrap properly while maintaining vertical alignment with the start of the message text.
- **UI Label Fix:** Fixed the button label in the Settings Window from "Save & Close" to "Close".

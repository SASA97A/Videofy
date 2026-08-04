# Videofy - Release Notes

## Recent Features & Enhancements

### 🚀 Drag & Drop Support (Cross-Platform)
- **Cross-Platform Drag & Drop:** Easily drag and drop video files or entire folders into Videofy from Windows Explorer, macOS Finder, or Linux file managers.
- **Visual Drop Overlay:** Full-window drag overlay automatically appears when files are dragged over the window, highlighting supported video formats.
- **Recursive & Multi-Location Scanning:** Dropping folders recursively scans subdirectories for videos while preserving existing items in your queue.

### 🎨 Modern Bottom Status Bar & Card-Based UI Layout
- **Global Bottom Status Bar:** Status indicators (Folder/Location, Selection count, Total Size, Processing Status & Speed) are now permanently pinned at the bottom status bar across all tabs (Notepad++ / IDE style).
- **Card-Based Control Panels:** Settings in OPTIMIZE, CONVERT, and SPLIT tabs are organized into clean, elevated visual cards to eliminate clutter.
- **Interactive Output Format Dropdown:** You can now select the output format (.mp4, .mkv, .avi, .mov, .webm) directly from the OPTIMIZE tab. Defaults to your global settings preference.
- **Enhanced Quality Slider:** Added clear visual endpoint guides ("Smaller File Size" vs "Higher Quality") and live CRF indicators.

### 🛠️ Multi-Location Tracking & Bug Fixes
- **Multi-Location Refreshing:** Refactored **Refresh List** to verify and refresh multi-folder files and single dropped files without rescanning unrelated parent folders.
- **Targeted Output Tracking:** Compressed output files automatically remain tracked and verified across list refreshes.
- **Trim Badge Fix:** Fixed an issue where compressed files falsely displayed a `00:00 - 00:00` trim badge after probing file duration.

### 🎥 Container-Aware Compression & Resilient Stream Fallback
- **Smart Container Compatibility:** MKV outputs retain 1:1 stream passthrough (video, audio, text/bitmap subtitles, and font attachments). Non-MKV outputs (MP4/MOV) convert text subtitles to `mov_text` and omit unsupported font attachments.
- **Zero-Failure Resilient Fallback:** If stream copying fails (e.g., incompatible DTS/FLAC audio in MP4), FFmpeg automatically logs a warning, cleans up, and retries with high-quality AAC audio encoding without interrupting the batch queue.
- **Default Metadata Handling:** "Remove video trackers & hidden metadata" is now unchecked by default to preserve audio/subtitle track names, titles, and language labels.
- **Pre-Flight UI Hints:** Added non-blocking info notice for MP4/MOV output selection and clear warnings when enabling metadata stripping.

### 👁️ High-Contrast UI & Accessibility Overhaul
- **WCAG AAA Contrast Palette:** Overhauled global colors (`#14171A` background, `#1E2228` cards, `#282D35` inputs, `#424954` borders) to achieve 4.8:1+ contrast ratios for cards and 7.5:1+ contrast for text.
- **Enhanced Typography Scaling:** Increased secondary text, label, and slider endpoint font sizes to 11px–13px for crisp legibility on 1080p, 1440p, and 4K displays.
- **Encapsulated Warning & Info Cards:** Warnings (metadata removal) and info hints (MP4 stream conversion) are now styled in padded amber and sky blue card banners with high-contrast text.

### 🔄 Automatic Startup Update Check & Status Bar Notification
- **Silent Background Auto-Check:** Videofy automatically checks for new releases on GitHub 1.5 seconds after launch without blocking startup or popping up error dialogs when offline.
- **Status Bar Notification Pill:** When a new release is detected, a clean, high-contrast green pill (`Update Available: vX.X.X`) appears on the bottom status bar. Clicking it prompts to view details or download.
- **Global Settings Toggle:** Added an *"Automatically check for updates on startup"* option in Global Settings (enabled by default).
- **Refactored DRY Architecture:** Single refactored update checking engine shared between silent startup checks and manual checks.

### Human-Readable Log System & Severity-Coded Log Viewer
- **Emoji-Free Standardized ASCII Tags:** Replaced unicode emojis (`✔`, `⚠`, `✖`, `…`, `•`) with clean ASCII tags (`[ERROR]`, `[WARN]`, `[SUCCESS]`, `[INFO]`, `[DEBUG]`) across UI logs and `app_logs.txt`.
- **Severity Color-Coded Log Viewer:** Redesigned `LogWindow.axaml` with a virtualized log entry list featuring distinct severity color badges: Crimson Red for Errors, Amber for Warnings, Emerald Green for Success, Cyan for Scopes, and styled visual card banners for section headers.
- **Log Filtering & Toolbar:** Added log filtering selector (`All Logs`, `Errors Only`, `Warnings & Errors`), a one-click **Copy Logs** button, a **Clear View** button, and log file size tracking.
- **Enhanced Exception Audit:** Upgraded exception handling in `SettingsService`, `FfmpegService`, `FfprobeService`, and `SystemUtilityService` to capture and log detailed context instead of swallowing errors.

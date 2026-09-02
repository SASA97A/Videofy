# Spec: GPU Hardware Auto-Detection & Options Validation

## 1. Overview
This specification details the GPU hardware auto-detection feature introduced in Videofy v1.4.3. The app will automatically test and identify supported GPU encoders (NVIDIA NVENC, AMD AMF, Intel QSV) on Windows, macOS, and Linux. In the Settings UI, unsupported hardware options will be disabled (grayed out) until auto-detection confirms they work on the user's system.

## 2. Requirements & Constraints
- **Multiplatform Support:** Works across Windows, macOS, and Linux by utilizing appropriate platform null devices (`NUL` on Windows, `/dev/null` on Linux/macOS).
- **Silent Verification:** Hardware testing must run silently via a dummy 256x256 0.1s video render in FFmpeg without popping up console windows or interrupting the user.
- **Async Execution:** Must run asynchronously so startup and UI responsiveness are never blocked.
- **Always-Available Fallback:** The CPU encoder `Standard (Slow, Best Quality)` (`libx265`) remains enabled and available at all times.
- **UI State Management:** In the Settings Window, only supported encoders are enabled (`IsEnabled="True"`). Unsupported encoders are disabled (`IsEnabled="False"`). A manual "Auto-detect" button allows users to re-scan hardware on demand.

## 3. Architecture & Data Model

### Data Model (`AppSettings.cs`)
```csharp
public List<string> EnabledEncoders { get; set; } = new() { "Standard (Slow, Best Quality)" };
public List<string> SupportedHardwareEncoders { get; set; } = new();
public bool HasDetectedHardware { get; set; } = false;
```

### Probe Service (`FfmpegService.cs`)
- `TestEncoderAsync(string encoder)`: Runs a lightweight FFmpeg command testing a specific encoder. Returns `true` if exit code is 0, else `false`.
- `DetectSupportedHardwareEncodersAsync()`: Iterates through hardware encoder mappings in `AppConstants.EncoderMap` and tests each one.

### View Model & UI Binding (`SettingsViewModel.cs` & `SettingsWindow.axaml`)
- `EncoderOption` class updated with `IsSupported` property.
- `CheckBox.IsEnabled` in `SettingsWindow.axaml` bound to `IsSupported`.
- `AutoDetectCommand` added to `SettingsViewModel` to trigger detection and update `IsSupported` and `IsIncluded` dynamically.

### App Startup (`MainWindowViewModel.cs`)
- On application launch, if `HasDetectedHardware` is `false` and FFmpeg binaries are verified:
  1. Spawns background hardware detection task (`Task.Run`).
  2. Auto-enables all detected supported encoders in `EnabledEncoders` and `SupportedHardwareEncoders`.
  3. Sets `HasDetectedHardware = true` and saves settings to disk.
  4. Updates UI properties (`AvailableEncoders`).

## 4. Testing & Verification Strategy
1. **Unit/Integration Test:** Run silent probes directly using `FfmpegService` to confirm detection accurately identifies NVENC/AMF/QSV availability.
2. **Settings UI Verification:** Confirm disabled state for non-existent GPU hardware encoders and enabled state for present hardware encoders.
3. **Version Increment Check:** Confirm `AppConstants.AppVersion` is `"v1.4.3"`.

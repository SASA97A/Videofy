# Spec: Release Notes & Feature Branch Merge to Version-1.4.3

## 1. Overview
This specification covers merging the `feature/hardware-detection` branch into `Version-1.4.3` and creating the `releasenotes.md` document for Videofy v1.4.3.

## 2. Requirements
1. **Branch Merge:** Merge `feature/hardware-detection` into `Version-1.4.3` locally.
2. **Release Notes File:** Create `releasenotes.md` at the project root containing:
   - Details of the GPU Hardware Auto-Detection feature (NVENC, AMF, QSV across Windows, macOS, Linux).
   - Details of the updated Settings UI (disabled unsupported options, Auto-detect button).
3. **Verification:** Verify that `dotnet build` succeeds after merging and creating the file.
4. **Git Commit:** Commit `releasenotes.md` to `Version-1.4.3`.

## 3. Implementation Steps
1. Checkout branch `Version-1.4.3`.
2. Merge `feature/hardware-detection` into `Version-1.4.3`.
3. Create `releasenotes.md`.
4. Run `dotnet build "Video Size Optimizer/Video Size Optimizer.csproj"`.
5. Stage and commit `releasenotes.md`.

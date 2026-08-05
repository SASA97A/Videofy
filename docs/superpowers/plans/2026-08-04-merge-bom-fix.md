# UTF-8 BOM Removal for FFmpeg Temp Files Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove UTF-8 BOM from FFmpeg temp files (`ffmetadata.txt` and `concat_list.txt`) to fix error exit code `-541478725`.

**Architecture:** Update `StreamWriter` constructor parameters in `FfmpegService.cs` to use `new System.Text.UTF8Encoding(false)`.

**Tech Stack:** C# .NET 9.

## Global Constraints
- Branch: `feature/smart-video-merge`

---

### Task 1: Update StreamWriter Encoding in FfmpegService

**Files:**
- Modify: `Video Size Optimizer/Services/FfmpegService.cs:447,480`

**Interfaces:**
- Consumes: `FfmpegService.GenerateChapterFile`, `FfmpegService.MergeVideosAsync`
- Produces: BOM-less UTF-8 text files for FFmpeg.

- [ ] **Step 1: Update encoding in `GenerateChapterFile`**

Replace:
`using var writer = new StreamWriter(metaFile, false, System.Text.Encoding.UTF8);`
With:
`using var writer = new StreamWriter(metaFile, false, new System.Text.UTF8Encoding(false));`

- [ ] **Step 2: Update encoding in `MergeVideosAsync`**

Replace:
`using (var writer = new StreamWriter(listFile, false, System.Text.Encoding.UTF8))`
With:
`using (var writer = new StreamWriter(listFile, false, new System.Text.UTF8Encoding(false)))`

- [ ] **Step 3: Verify build**

Run: `dotnet build "Video Size Optimizer/Video Size Optimizer.csproj"`
Expected: Build succeeded with 0 Errors.

- [ ] **Step 4: Commit**

```bash
git add "Video Size Optimizer/Services/FfmpegService.cs"
git commit -m "fix(merge): remove UTF-8 BOM from chapter and concat list files to fix FFmpeg crash"
```

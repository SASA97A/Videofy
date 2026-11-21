using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Video_Size_Optimizer.Services;
using Video_Size_Optimizer.Utils;


public struct ConversionProgress
{
    public double Percentage;
    public double CurrentSeconds;
    public string Speed; // e.g., "1.2x"
    public string Fps;   // e.g., "45"
}

public class FfmpegService
{
    private readonly string _ffmpegPath;
    private static bool _initialized = false;
    private static readonly object _lock = new();
    private Process? _currentProcess;
    // Set by KillProcess (user pressed Stop). A stopped run is not a failure: it returns quietly and
    // no further ffmpeg step (next pass, subtitle retry, next merge step) is started.
    private volatile bool _stopRequested;

    /// <summary>Call at the start of a batch so an earlier Stop does not block the new run.</summary>
    public void ClearStopRequest() => _stopRequested = false;

    // Pause for Win
    [DllImport("ntdll.dll")] private static extern int NtSuspendProcess(IntPtr processHandle);
    [DllImport("ntdll.dll")] private static extern int NtResumeProcess(IntPtr processHandle);

    public FfmpegService()
    {
        _ffmpegPath = AppPathService.FfmpegExecutable;
    }

    public void TogglePause(bool isPaused)
    {
        if (_currentProcess == null || _currentProcess.HasExited) return;

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (isPaused) NtSuspendProcess(_currentProcess.Handle);
                else NtResumeProcess(_currentProcess.Handle);
            }
            else // Linux & Mac
            {
                var signal = isPaused ? "-STOP" : "-CONT";
                Process.Start("kill", $"{signal} {_currentProcess.Id}");
            }
        }
        catch (Exception ex)
        {
            LogService.Instance.Log(
                $"Failed to toggle pause. Paused={isPaused} | {ex.Message}", LogLevel.Error, "FFMPEG");
        }
    }

    public string? InitializePermissions()
    {
        if (_initialized || RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return null;

        lock (_lock)
        {
            if (_initialized) return null;

            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    using var process = Process.Start("chmod", $"+x \"{_ffmpegPath}\"");
                    process?.WaitForExit();
                }

                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    var xattrArgs = $"-dr com.apple.quarantine \"{_ffmpegPath}\"";
                    using var xattrProcess = Process.Start("xattr", xattrArgs);
                    xattrProcess?.WaitForExit();
                }

                _initialized = true;
                return null;
            }
            catch (Exception ex)
            {
                LogService.Instance.Log(
                    $"Permission initialization failed. Path={_ffmpegPath} | {ex.Message}", LogLevel.Error, "FFMPEG");

                return "Videofy doesn't have permission to run FFmpeg.\n\n" +
                       "The FFmpeg binary needs execution rights to work.\n\n" +
                       "How to fix:\n" +
                       "1. Open your Terminal\n" +
                       $"2. Run: chmod +x \"{_ffmpegPath}\"\n\n" +
                       "Then try clicking Start again.";
            }
        }
    }

    public async Task CompressAsync(string input, string output, string targetFps, bool stripMetadata, int crf, string encoder, string selectedResolution, string trimArgs, int? maxBitrateKbps = null, IProgress<ConversionProgress>? progress = null)
    {
        try
        {
            if (encoder == "copy")
            {
                var copyArgs = $"-y {trimArgs} -i {ProcessArgs.Quote(input)} -c copy -map 0 {ProcessArgs.Quote(output)}";
                try
                {
                    await RunFfmpegProcessAsync(copyArgs, progress);
                    return;
                }
                catch (Exception ex)
                {
                    LogService.Instance.Log($"[FALLBACK] Direct stream copy failed for {output}. Retrying with AAC audio fallback... | Error: {ex.Message}", LogLevel.Warning, "FFMPEG");
                    if (File.Exists(output))
                    {
                        try { File.Delete(output); } catch { }
                    }
                    var fallbackCopyArgs = $"-y {trimArgs} -i {ProcessArgs.Quote(input)} -c:v copy -c:a aac -b:a 192k -dn {ProcessArgs.Quote(output)}";
                    await RunFfmpegProcessAsync(fallbackCopyArgs, progress);
                    return;
                }
            }

            var filters = new List<string>();

            if (!string.IsNullOrEmpty(selectedResolution) && selectedResolution != "Original")
            {
                // Extract the width (e.g., from "1920 (1080p)")
                string width = selectedResolution.Split(' ')[0];
                filters.Add($"scale={width}:-2");
            }
            if (targetFps != "Original" && int.TryParse(targetFps, out int fpsValue))
            {
                // -fps_max caps the framerate without forcing a specific number if it's lower
                filters.Add($"fps={fpsValue}");
            }

            string codecArgs;
            string metadataFlag = stripMetadata ? "-map_metadata -1 -map_chapters -1" : "";
            string bitrateCap = maxBitrateKbps.HasValue
            ? $"-maxrate {maxBitrateKbps}k -bufsize {maxBitrateKbps * 2}k"
            : "";

            if (encoder.Contains("nvenc"))
            {
                codecArgs = $"-c:v {encoder} -preset p5 -rc vbr -cq {crf} {bitrateCap}";
            }
            else if (encoder.Contains("amf"))
            {
                codecArgs = $"-c:v {encoder} -rc vbr_peak -qp_i {crf} -qp_p {crf} -quality quality {bitrateCap}";
            }
            else if (encoder.Contains("qsv"))
            {
                codecArgs = $"-c:v {encoder} -preset veryfast -global_quality {crf} {bitrateCap}";
            }
            else
            {
                // Standard CPU (x265) 
                codecArgs = $"-c:v libx265 -crf {crf} {bitrateCap}";
            }

            string filterArgs = filters.Count > 0 ? $"-vf \"{string.Join(",", filters)}\"" : "";
            
            string extension = Path.GetExtension(output).ToLowerInvariant();
            bool isMkv = extension == ".mkv";

            // Format-aware stream arguments:
            // MKV supports full stream passthrough (-c:a copy -c:s copy -c:t copy).
            // MP4/MOV do not support font attachments (-c:t) or raw PGS/ASS subtitles, so we convert text subtitles to mov_text and omit attachments.
            string streamArgs = isMkv
                ? "-c:a copy -c:s copy -c:t copy"
                : "-c:a copy -c:s mov_text";

            var args = $"-y {trimArgs} -i {ProcessArgs.Quote(input)} -map 0 {filterArgs} {codecArgs} {streamArgs} -dn {metadataFlag} {ProcessArgs.Quote(output)}";

            try
            {
                await RunFfmpegProcessAsync(args, progress);
            }
            catch (Exception ex)
            {
                LogService.Instance.Log($"[FALLBACK] Compression failed with primary stream args for {output}. Attempting resilient AAC audio fallback... | Error: {ex.Message}", LogLevel.Warning, "FFMPEG");

                if (File.Exists(output))
                {
                    try { File.Delete(output); } catch { }
                }

                // Fallback attempt: Transcode audio to high-quality AAC (192k) and drop incompatible streams
                var fallbackArgs = $"-y {trimArgs} -i {ProcessArgs.Quote(input)} {filterArgs} {codecArgs} -c:a aac -b:a 192k -dn {metadataFlag} {ProcessArgs.Quote(output)}";
                await RunFfmpegProcessAsync(fallbackArgs, progress);
            }
        }
        catch (Exception ex)
        {
            LogService.Instance.Log(
                $"Compress/Encoding failed. Input={input} | Output={output} | {ex.Message}", LogLevel.Error, "FFMPEG");

            // A failed encode can leave a file that looks finished but is broken (seen with a damaged
            // recording: 107 video frames in 20 minutes). Delete it, as target size does.
            await DeleteWithRetryAsync(output);
            throw;
        }      
    }

    // Smart target size  
    public async Task CompressTargetSizeAsync(string input, string output, string targetFps, bool stripMetadata, int targetMb, string encoder, string selectedResolution, double duration, string trimArgs, int audioTrackCount = 1, IProgress<ConversionProgress>? progress = null)
    {
        // Bitrate = (Size in MB * 8192) / Duration
        // Every audio track is kept and encoded at 128 kbps, so reserve 128 kbps per track.
        const int AudioKbpsPerTrack = 128;
        int audioTracks = Math.Max(0, audioTrackCount);
        double totalBitrate = (targetMb * 8192.0) / duration;
        int videoBitrate = (int)(totalBitrate - AudioKbpsPerTrack * audioTracks);
        if (videoBitrate < 100) videoBitrate = 100; // Safety floor

        // Keep the first video, every audio track and the subtitles, as normal compress mode does.
        // Without -map, ffmpeg kept only one audio track and dropped all subtitles.
        // MP4 cannot hold every subtitle format, so text subtitles become mov_text; MKV copies them and its attachments (fonts).
        string audioArgs = $"-c:a aac -b:a {AudioKbpsPerTrack}k";
        bool isMkv = Path.GetExtension(output).Equals(".mkv", StringComparison.OrdinalIgnoreCase);
        string fullStreamArgs = isMkv
            ? $"-map 0:v:0 -map 0:a? -map 0:s? -map 0:t? {audioArgs} -c:s copy -c:t copy"
            : $"-map 0:v:0 -map 0:a? -map 0:s? {audioArgs} -c:s mov_text";
        // Used if the subtitles cannot be written (for example picture-based PGS subtitles into MP4).
        string fallbackStreamArgs = $"-map 0:v:0 -map 0:a? {audioArgs}";

        var filters = new List<string>();
        if (!string.IsNullOrEmpty(selectedResolution) && selectedResolution != "Original")
            filters.Add($"scale={selectedResolution.Split(' ')[0]}:-2");
        if (targetFps != "Original" && int.TryParse(targetFps, out int fpsValue))
            filters.Add($"fps={fpsValue}");

        string filterArgs = filters.Count > 0 ? $"-vf \"{string.Join(",", filters)}\"" : "";
        // Used by the pass that writes the output (pass 2, or the single GPU pass). Pass 1 output is discarded.
        string metadataFlag = stripMetadata ? "-map_metadata -1 -map_chapters -1" : "";
        string logName = Path.Combine(Path.GetTempPath(), $"ffmpeg2pass_{Guid.NewGuid()}");
        string nullDev = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "NUL" : "/dev/null";

        // Pass 1: 0% -> 50%
        var p1 = new Progress<ConversionProgress>(cp => progress?.Report(new ConversionProgress
        {
            Percentage = cp.Percentage * 0.5,
            Speed = cp.Speed,
            Fps = cp.Fps
        }));
        // Pass 2: 50% -> 100%
        var p2 = new Progress<ConversionProgress>(cp => progress?.Report(new ConversionProgress
        {
            Percentage = 50 + (cp.Percentage * 0.5),
            Speed = cp.Speed,
            Fps = cp.Fps
        }));

        // PASS 1
        var pass1 =$"-y {trimArgs} -i {ProcessArgs.Quote(input)} {filterArgs} -c:v {encoder} -b:v {videoBitrate}k -pass 1 -passlogfile {ProcessArgs.Quote(logName)} -an -f null {nullDev}";
        // PASS 2
        string Pass2(string streamArgs) =>
            $"-y {trimArgs} -i {ProcessArgs.Quote(input)} {filterArgs} -c:v {encoder} -b:v {videoBitrate}k -pass 2 -passlogfile {ProcessArgs.Quote(logName)} {streamArgs} {metadataFlag} {ProcessArgs.Quote(output)}";

        // Runs the step that writes the output file. If it fails with subtitles included,
        // removes the partial file and tries once more with video and audio only.
        async Task RunOutputStepAsync(Func<string, string> buildArgs, IProgress<ConversionProgress>? stepProgress)
        {
            try
            {
                await RunFfmpegProcessAsync(buildArgs(fullStreamArgs), stepProgress);
            }
            // Never retry after Stop: that would start ffmpeg again after the user stopped it.
            catch (Exception ex) when (!_stopRequested)
            {
                LogService.Instance.Log(
                    $"[FALLBACK] Target-size output failed with subtitles for {output}. Retrying without subtitles... | Error: {ex.Message}",
                    LogLevel.Warning, "FFMPEG");
                if (File.Exists(output))
                {
                    try { File.Delete(output); } catch { }
                }
                await RunFfmpegProcessAsync(buildArgs(fallbackStreamArgs), stepProgress);
            }
        }

        // GPU encoders (NVENC, AMF, QSV) ignore -pass: tested with hevc_nvenc, the "pass 2" output was
        // byte-identical to a single-pass encode, so pass 1 only doubled the time. They run one pass
        // with their own built-in analysis instead, capped by -maxrate so the file stays under the target.
        // Measured on a 60 s 1440p clip with a 25 MB target: old 2-pass 16.6 s and 103% of target;
        // NVENC multipass 13.8 s and 99% of target, at nearly the same quality.
        bool isGpuEncoder = encoder.Contains("nvenc") || encoder.Contains("amf") || encoder.Contains("qsv");

        try
        {
            if (isGpuEncoder)
            {
                string rateArgs = $"-b:v {videoBitrate}k -maxrate {videoBitrate}k -bufsize {videoBitrate * 2}k";
                string gpuArgs = encoder.Contains("nvenc")
                    ? $"-preset p5 -rc vbr -multipass fullres -rc-lookahead 32 {rateArgs}"
                    : encoder.Contains("amf")
                        ? $"-rc vbr_peak -quality quality {rateArgs}"
                        : $"-preset veryfast {rateArgs}";

                string SinglePass(string streamArgs) =>
                    $"-y {trimArgs} -i {ProcessArgs.Quote(input)} {filterArgs} -c:v {encoder} {gpuArgs} {streamArgs} {metadataFlag} {ProcessArgs.Quote(output)}";

                LogService.Instance.Log($"Single pass (GPU) start | Encoder={encoder} | VideoBitrate={videoBitrate}k | AudioTracks={audioTracks}", LogLevel.Info, "FFMPEG");
                await RunOutputStepAsync(SinglePass, progress);
                if (_stopRequested) return;
                LogService.Instance.Log("Single pass (GPU) complete", LogLevel.Info, "FFMPEG");
            }
            else
            {
                LogService.Instance.Log($"Two-pass (CPU) | Encoder={encoder} | VideoBitrate={videoBitrate}k | AudioTracks={audioTracks}", LogLevel.Info, "FFMPEG");
                // Log each pass when it really runs, so a failure shows which pass it was in.
                LogService.Instance.Log("Pass 1/2 start (analysis)", LogLevel.Info, "FFMPEG");
                await RunFfmpegProcessAsync(pass1, p1);
                // Stopped in pass 1: its stats file is incomplete, so pass 2 must not start.
                if (_stopRequested) return;
                LogService.Instance.Log("Pass 1/2 done", LogLevel.Info, "FFMPEG");

                LogService.Instance.Log("Pass 2/2 start (output)", LogLevel.Info, "FFMPEG");
                await RunOutputStepAsync(Pass2, p2);
                if (_stopRequested) return;
                LogService.Instance.Log("Pass 2/2 done", LogLevel.Info, "FFMPEG");
            }
        }
        catch (Exception ex)
        {
            LogService.Instance.Log(
                $"Target-size compression failed. Input={input} | Output={output} | Target={targetMb}MB | Error: {ex.Message}", LogLevel.Error, "FFMPEG");

            // A failed pass 2 can leave a partial output file. Remove it so it is never mistaken for a finished encode.
            if (File.Exists(output))
            {
                try { File.Delete(output); }
                catch (Exception deleteEx)
                {
                    LogService.Instance.Log($"Failed to delete partial output {output} | {deleteEx.Message}", LogLevel.Error, "FFMPEG");
                }
            }

            // Let the caller know the encode failed, so it does not count it as done or delete the original.
            throw;
        }
        finally
        {
            // libx265 writes its CU-tree data next to the stats file (can be tens of MB).
            // x264/x265 write to ".temp" files and rename them when pass 1 ends, so a stopped or failed
            // pass 1 leaves the ".temp" names behind (seen: 27 MB .cutree.temp).
            string[] suffixes =
            {
                "-0.log", "-0.log.mbtree", "-0.log.cutree",
                "-0.log.temp", "-0.log.mbtree.temp", "-0.log.cutree.temp", ".log"
            };
            foreach (var suffix in suffixes)
                await DeleteWithRetryAsync($"{logName}{suffix}");
        }
    }

    // Right after Stop, Windows can keep the killed ffmpeg's files locked for a moment, so retry briefly.
    public static async Task DeleteWithRetryAsync(string path)
    {
        for (int attempt = 1; File.Exists(path); attempt++)
        {
            try
            {
                File.Delete(path);
                return;
            }
            catch (IOException) when (attempt < 10)
            {
                await Task.Delay(200);
            }
            catch (Exception ex)
            {
                LogService.Instance.Log($"Failed to delete {path} | {ex.Message}", LogLevel.Error, "FFMPEG");
                return;
            }
        }
    }

    //Split Video
    public async Task SplitVideoAsync(string input, string outputPattern, string splitArgs, IProgress<ConversionProgress>? progress = null)
    {

        try
        {
            var args = $"-y -i {ProcessArgs.Quote(input)} -c copy -map 0 {splitArgs} {ProcessArgs.Quote(outputPattern)}";
            await RunFfmpegProcessAsync(args, progress);
        }
        catch (Exception ex)
        {
            LogService.Instance.Log(
                $"Split failed. Input={input} | OutputPattern={outputPattern} | Error: {ex.Message}", LogLevel.Error, "FFMPEG");
            throw;
        }
    }

    private async Task RunFfmpegProcessAsync(string args, IProgress<ConversionProgress>? progress, double? knownTotalDuration = null)
    {
        if (!File.Exists(_ffmpegPath)) throw new FileNotFoundException("FFmpeg not found", _ffmpegPath);

        // Stop was pressed between steps: do not start another ffmpeg run.
        if (_stopRequested)
        {
            LogService.Instance.Log("Step skipped: stopped by user.", LogLevel.Info, "FFMPEG");
            return;
        }

        try
        {
            _currentProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _ffmpegPath,
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true
                }
            };

            double totalDuration = knownTotalDuration ?? 0;
            _currentProcess.Start();
            ChildProcessGuard.Track(_currentProcess);

            using (var reader = _currentProcess.StandardError)
            {
                while (await reader.ReadLineAsync() is string line)
                {
                    if (totalDuration == 0)
                    {
                        var match = Regex.Match(line, @"Duration:\s(\d+):(\d+):(\d+\.\d+)");
                        if (match.Success)
                            totalDuration = (double.Parse(match.Groups[1].Value) * 3600) + (double.Parse(match.Groups[2].Value) * 60) + double.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
                    }

                    var timeMatch = Regex.Match(line, @"time=(\d+):(\d+):(\d+\.\d+)");
                    //var speedMatch = Regex.Match(line, @"speed=\s*(\d+\.\d+x)"); strict  
                    var speedMatch = Regex.Match(line, @"speed=\s*([\d\.]+x)");
                    // var fpsMatch = Regex.Match(line, @"fps=\s*(\d+)");  strict
                    var fpsMatch = Regex.Match(line, @"fps=\s*([\d\.]+)");

                    if (timeMatch.Success && totalDuration > 0 && progress != null)
                    {
                        double currentSeconds = (double.Parse(timeMatch.Groups[1].Value) * 3600) + (double.Parse(timeMatch.Groups[2].Value) * 60) + double.Parse(timeMatch.Groups[3].Value, CultureInfo.InvariantCulture);
                        progress.Report(new ConversionProgress
                        {
                            Percentage = Math.Clamp((currentSeconds / totalDuration) * 100, 0, 100),
                            CurrentSeconds = currentSeconds,
                            Speed = speedMatch.Success ? speedMatch.Groups[1].Value : "0x",
                            Fps = fpsMatch.Success ? fpsMatch.Groups[1].Value : "0"
                        });
                    }
                }
            }

            await _currentProcess.WaitForExitAsync();

            // A run killed by Stop exits with -1 on Windows and 137 on Linux/macOS. Detect it with the flag,
            // not the exit code: ffmpeg's own "Operation not permitted" error is also -1.
            if (_stopRequested)
            {
                LogService.Instance.Log($"Stopped by user (exit code {_currentProcess.ExitCode}).", LogLevel.Info, "FFMPEG");
                return;
            }

            if (_currentProcess.ExitCode != 0)
                throw new Exception($"FFmpeg failed with exit code {_currentProcess.ExitCode}");
        }
        catch (Exception ex)
        {
            LogService.Instance.Log(
                $"Execution failed. Args={args} | {ex.Message}", LogLevel.Error, "FFMPEG");
            throw;
        }
        finally
        {
            _currentProcess = null;
        }
    }

    public void KillProcess()
    {
        // Set before the kill, so the exit of the killed process is already seen as a stop.
        _stopRequested = true;
        try
        {
            if (_currentProcess != null && !_currentProcess.HasExited)
            {
                _currentProcess.Kill(true);
            }
        }
        catch (Exception ex)
        {
            LogService.Instance.Log(
                $"Failed to kill ffmpeg process | {ex.Message}", LogLevel.Error, "FFMPEG");
        }
    }

    public async Task<bool> TestEncoderAsync(string encoder)
    {
        if (!File.Exists(_ffmpegPath)) return false;

        try
        {
            string nullDev = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "NUL" : "/dev/null";
            var args = $"-y -f lavfi -i color=c=black:s=256x256:d=0.1 -c:v {encoder} -f null {nullDev}";
            
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _ffmpegPath,
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                }
            };

            process.Start();

            ChildProcessGuard.Track(process);
            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public async Task<List<string>> DetectSupportedHardwareEncodersAsync()
    {
        var supported = new List<string>();
        foreach (var kvp in Video_Size_Optimizer.Utils.AppConstants.EncoderMap)
        {
            if (kvp.Key.Contains("Standard")) continue;
            if (await TestEncoderAsync(kvp.Value))
            {
                supported.Add(kvp.Key);
            }
        }
        return supported;
    }

    public bool CheckMergeCompatibility(List<VideoMetadata> metadataList)
    {
        if (metadataList == null || metadataList.Count < 2) return false;
        var first = metadataList[0];
        for (int i = 1; i < metadataList.Count; i++)
        {
            var v1 = first.Video;
            var v2 = metadataList[i].Video;
            var a1 = first.Audio;
            var a2 = metadataList[i].Audio;

            if (v1.Codec != v2.Codec || v1.Width != v2.Width || v1.Height != v2.Height ||
                Math.Abs(v1.Fps - v2.Fps) > 0.05 || v1.PixFmt != v2.PixFmt)
                return false;

            if (a1.Exists != a2.Exists || a1.SampleRate != a2.SampleRate || a1.Channels != a2.Channels)
                return false;
        }
        return true;
    }

    public string GenerateChapterFile(List<VideoMetadata> metadataList)
    {
        string metaFile = Path.Combine(Path.GetTempPath(), $"ffmetadata_{Guid.NewGuid()}.txt");
        using var writer = new StreamWriter(metaFile, false, new System.Text.UTF8Encoding(false));
        writer.WriteLine(";FFMETADATA1");

        double currentTime = 0.0;
        for (int i = 0; i < metadataList.Count; i++)
        {
            long startMs = (long)(currentTime * 1000);
            long durationMs = (long)(metadataList[i].Duration * 1000);
            long endMs = startMs + durationMs;
            string title = Path.GetFileNameWithoutExtension(metadataList[i].Path);

            writer.WriteLine("[CHAPTER]");
            writer.WriteLine("TIMEBASE=1/1000");
            writer.WriteLine($"START={startMs}");
            writer.WriteLine($"END={endMs}");
            writer.WriteLine($"title=Part {i + 1}: {ProcessArgs.EscapeMetadata(title)}");
            writer.WriteLine();

            currentTime += metadataList[i].Duration;
        }

        return metaFile;
    }

    public async Task MergeVideosAsync(List<VideoMetadata> metadataList, string outputPath, bool forceReencode, string encoder, IProgress<ConversionProgress>? progress = null)
    {
        if (metadataList == null || metadataList.Count < 2) return;

        bool isCompatible = CheckMergeCompatibility(metadataList);
        string chapterFile = GenerateChapterFile(metadataList);
        double groupTotalDuration = metadataList.Sum(m => m.Duration);

        try
        {
            if (isCompatible && !forceReencode)
            {
                LogService.Instance.Log("Streams are compatible. Using Lossless Concat Demuxer (-c copy)...", LogLevel.Info, "MERGE");
                string listFile = Path.Combine(Path.GetTempPath(), $"concat_{Guid.NewGuid()}.txt");
                using (var writer = new StreamWriter(listFile, false, new System.Text.UTF8Encoding(false)))
                {
                    foreach (var meta in metadataList)
                    {
                        string safePath = meta.Path.Replace("'", "'\\''");
                        writer.WriteLine($"file '{safePath}'");
                    }
                }

                var copyArgs = $"-y -f concat -safe 0 -i {ProcessArgs.Quote(listFile)} -i {ProcessArgs.Quote(chapterFile)} -map_metadata 1 -c copy {ProcessArgs.Quote(outputPath)}";
                try
                {
                    await RunFfmpegProcessAsync(copyArgs, progress, groupTotalDuration);
                }
                finally
                {
                    if (File.Exists(listFile)) File.Delete(listFile);
                }
            }
            else
            {
                LogService.Instance.Log("Re-encoding required. Building dynamic canvas filtergraph...", LogLevel.Info, "MERGE");
                int maxW = metadataList.Max(m => m.Video.Width);
                int maxH = metadataList.Max(m => m.Video.Height);
                if (maxW % 2 != 0) maxW++;
                if (maxH % 2 != 0) maxH++;
                double maxFps = metadataList.Max(m => m.Video.Fps);

                var filterChains = new List<string>();
                var inputArgs = new List<string>();

                for (int i = 0; i < metadataList.Count; i++)
                {
                    var meta = metadataList[i];
                    inputArgs.Add($"-i {ProcessArgs.Quote(meta.Path)}");

                    string vFilter = $"[{i}:v]scale=w='if(gt(iw/ih,{maxW}/{maxH}),{maxW},-2)':h='if(gt(iw/ih,{maxW}/{maxH}),-2,{maxH})':force_original_aspect_ratio=decrease," +
                                     $"pad=w={maxW}:h={maxH}:x='({maxW}-iw)/2':y='({maxH}-ih)/2':color=black," +
                                     $"fps={maxFps.ToString("F2", CultureInfo.InvariantCulture)},setsar=1[v{i}];";

                    string aFilter = meta.Audio.Exists
                        ? $"[{i}:a]aformat=sample_fmts=fltp:sample_rates=48000:channel_layouts=stereo[a{i}];"
                        : $"anullsrc=channel_layout=stereo:sample_rate=48000,atrim=duration={meta.Duration.ToString("F2", CultureInfo.InvariantCulture)}[a{i}];";

                    filterChains.Add(vFilter + aFilter);
                }

                string concatInputs = string.Join("", Enumerable.Range(0, metadataList.Count).Select(i => $"[v{i}][a{i}]"));
                string concatFilter = $"{concatInputs}concat=n={metadataList.Count}:v=1:a=1[vout][aout]";
                string fullFiltergraph = string.Join("", filterChains) + concatFilter;

                inputArgs.Add($"-i {ProcessArgs.Quote(chapterFile)}");

                string codecArgs;
                if (encoder.Contains("nvenc"))
                    codecArgs = $"-c:v {encoder} -preset p5 -rc vbr -cq 23";
                else if (encoder.Contains("amf"))
                    codecArgs = $"-c:v {encoder} -rc vbr_peak -qp_i 22 -qp_p 22";
                else if (encoder.Contains("qsv"))
                    codecArgs = $"-c:v {encoder} -preset veryfast -global_quality 23";
                else
                    codecArgs = $"-c:v {encoder} -crf 18";

                var args = $"-y {string.Join(" ", inputArgs)} -filter_complex \"{fullFiltergraph}\" -map \"[vout]\" -map \"[aout]\" -map_metadata {metadataList.Count} {codecArgs} -c:a aac -b:a 192k {ProcessArgs.Quote(outputPath)}";
                await RunFfmpegProcessAsync(args, progress, groupTotalDuration);
            }
        }
        finally
        {
            if (File.Exists(chapterFile)) File.Delete(chapterFile);
        }
    }
}

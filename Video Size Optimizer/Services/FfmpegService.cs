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
    public string Speed; // e.g., "1.2x"
    public string Fps;   // e.g., "45"
}

public class FfmpegService
{
    private readonly string _ffmpegPath;
    private static bool _initialized = false;
    private static readonly object _lock = new();
    private Process? _currentProcess;

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
                var copyArgs = $"-y {trimArgs} -i \"{input}\" -c copy -map 0 \"{output}\"";
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
                    var fallbackCopyArgs = $"-y {trimArgs} -i \"{input}\" -c:v copy -c:a aac -b:a 192k -dn \"{output}\"";
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
                codecArgs = $"-vcodec {encoder} -preset p5 -rc vbr -cq {crf} {bitrateCap}";
            }
            else if (encoder.Contains("amf"))
            {
                codecArgs = $"-vcodec {encoder} -rc vbr_peak -qp_i {crf} -qp_p {crf} -quality quality {bitrateCap}";
            }
            else if (encoder.Contains("qsv"))
            {
                codecArgs = $"-vcodec {encoder} -preset veryfast -global_quality {crf} {bitrateCap}";
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

            var args = $"-y {trimArgs} -i \"{input}\" -map 0 {filterArgs} {codecArgs} {streamArgs} -dn {metadataFlag} \"{output}\"";

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
                var fallbackArgs = $"-y {trimArgs} -i \"{input}\" {filterArgs} {codecArgs} -c:a aac -b:a 192k -dn {metadataFlag} \"{output}\"";
                await RunFfmpegProcessAsync(fallbackArgs, progress);
            }
        }
        catch (Exception ex)
        {
            LogService.Instance.Log(
                $"Compress/Encoding failed. Input={input} | Output={output} | {ex.Message}", LogLevel.Error, "FFMPEG");
            throw;
        }      
    }

    // Smart target size  
    public async Task CompressTargetSizeAsync(string input, string output, string targetFps, bool stripMetadata, int targetMb, string encoder, string selectedResolution, double duration, string trimArgs, IProgress<ConversionProgress>? progress = null)
    {
        // Bitrate = (Size in MB * 8192) / Duration
        // Subtracting 128kbps as a buffer for the audio stream
        double totalBitrate = (targetMb * 8192.0) / duration;
        int videoBitrate = (int)(totalBitrate - 128);
        if (videoBitrate < 100) videoBitrate = 100; // Safety floor

        var filters = new List<string>();
        if (!string.IsNullOrEmpty(selectedResolution) && selectedResolution != "Original")
            filters.Add($"scale={selectedResolution.Split(' ')[0]}:-2");
        if (targetFps != "Original" && int.TryParse(targetFps, out int fpsValue))
            filters.Add($"fps={fpsValue}");

        string filterArgs = filters.Count > 0 ? $"-vf \"{string.Join(",", filters)}\"" : "";
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
        LogService.Instance.Log("PASS 1 Start", LogLevel.Info, "FFMPEG");
        var pass1 = $"-y {trimArgs} -i \"{input}\" {filterArgs} -c:v {encoder} -b:v {videoBitrate}k -pass 1 -passlogfile \"{logName}\" -an -f null {nullDev}";
        // PASS 2
        LogService.Instance.Log("PASS 2 Start", LogLevel.Info, "FFMPEG");
        var pass2 = $"-y {trimArgs} -i \"{input}\" {filterArgs} -c:v {encoder} -b:v {videoBitrate}k -pass 2 -passlogfile \"{logName}\" -c:a aac -b:a 128k \"{output}\"";
        
        LogService.Instance.Log("PASS Complete", LogLevel.Info, "FFMPEG");

        try
        {
            await RunFfmpegProcessAsync(pass1, p1);
            await RunFfmpegProcessAsync(pass2, p2);
        }
        catch (Exception ex)
        {
            LogService.Instance.Log(
                $"Target-size compression failed. Input={input} | Output={output} | Target={targetMb}MB | Error: {ex.Message}", LogLevel.Error, "FFMPEG");
        }
        finally
        {
            try
            {
                if (File.Exists($"{logName}-0.log")) File.Delete($"{logName}-0.log");
                if (File.Exists($"{logName}-0.log.mbtree")) File.Delete($"{logName}-0.log.mbtree");
                if (File.Exists($"{logName}.log")) File.Delete($"{logName}.log");
            }
            catch (Exception ex)
            {
                LogService.Instance.Log(
                    $"Failed to clean 2-pass logs | {ex.Message}", LogLevel.Error, "FFMPEG");
            }
        }
    }

    //Split Video
    public async Task SplitVideoAsync(string input, string outputPattern, string splitArgs, IProgress<ConversionProgress>? progress = null)
    {

        try
        {
            var args = $"-y -i \"{input}\" -c copy -map 0 {splitArgs} \"{outputPattern}\"";
            await RunFfmpegProcessAsync(args, progress);
        }
        catch (Exception ex)
        {
            LogService.Instance.Log(
                $"Split failed. Input={input} | OutputPattern={outputPattern} | Error: {ex.Message}", LogLevel.Error, "FFMPEG");
            throw;
        }
    }

    private async Task RunFfmpegProcessAsync(string args, IProgress<ConversionProgress>? progress)
    {
        if (!File.Exists(_ffmpegPath)) throw new FileNotFoundException("FFmpeg not found", _ffmpegPath);

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

            double totalDuration = 0;
            _currentProcess.Start();

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
                            Speed = speedMatch.Success ? speedMatch.Groups[1].Value : "0x",
                            Fps = fpsMatch.Success ? fpsMatch.Groups[1].Value : "0"
                        });
                    }
                }
            }

            await _currentProcess.WaitForExitAsync();
            if (_currentProcess.ExitCode != 0 && _currentProcess.ExitCode != -1)
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
        using var writer = new StreamWriter(metaFile, false, System.Text.Encoding.UTF8);
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
            writer.WriteLine($"title=Part {i + 1}: {title}");
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

        try
        {
            if (isCompatible && !forceReencode)
            {
                LogService.Instance.Log("Streams are compatible. Using Lossless Concat Demuxer (-c copy)...", LogLevel.Info, "MERGE");
                string listFile = Path.Combine(Path.GetTempPath(), $"concat_{Guid.NewGuid()}.txt");
                using (var writer = new StreamWriter(listFile, false, System.Text.Encoding.UTF8))
                {
                    foreach (var meta in metadataList)
                    {
                        string safePath = meta.Path.Replace("'", "'\\''");
                        writer.WriteLine($"file '{safePath}'");
                    }
                }

                var copyArgs = $"-y -f concat -safe 0 -i \"{listFile}\" -i \"{chapterFile}\" -map_metadata 1 -c copy \"{outputPath}\"";
                try
                {
                    await RunFfmpegProcessAsync(copyArgs, progress);
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
                    inputArgs.Add($"-i \"{meta.Path}\"");

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

                inputArgs.Add($"-i \"{chapterFile}\"");

                string codecArgs;
                if (encoder.Contains("nvenc"))
                    codecArgs = $"-c:v {encoder} -preset p5 -rc vbr -cq 23";
                else if (encoder.Contains("amf"))
                    codecArgs = $"-c:v {encoder} -rc vbr_peak -qp_i 22 -qp_p 22";
                else if (encoder.Contains("qsv"))
                    codecArgs = $"-c:v {encoder} -preset veryfast -global_quality 23";
                else
                    codecArgs = $"-c:v {encoder} -crf 18";

                var args = $"-y {string.Join(" ", inputArgs)} -filter_complex \"{fullFiltergraph}\" -map \"[vout]\" -map \"[aout]\" -map_metadata {metadataList.Count} {codecArgs} -c:a aac -b:a 192k \"{outputPath}\"";
                await RunFfmpegProcessAsync(args, progress);
            }
        }
        finally
        {
            if (File.Exists(chapterFile)) File.Delete(chapterFile);
        }
    }
}

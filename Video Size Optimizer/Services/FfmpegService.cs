using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;


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
        var baseDir = AppContext.BaseDirectory;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            _ffmpegPath = Path.Combine(baseDir, "ffmpeg", "win-x64", "ffmpeg.exe");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            _ffmpegPath = Path.Combine(baseDir, "ffmpeg", "osx-x64", "ffmpeg");
        }
        else
        {
            _ffmpegPath = Path.Combine(baseDir, "ffmpeg", "linux-x64", "ffmpeg");
        }
    }

    public void TogglePause(bool isPaused)
    {
        if (_currentProcess == null || _currentProcess.HasExited) return;

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
            catch (Exception)
            {
                return "Videofy doesn't have permission to run FFmpeg.\n\n" +
                       "The FFmpeg binary needs execution rights to work.\n\n" +
                       "How to fix:\n" +
                       "1. Open your Terminal\n" +
                       $"2. Run: chmod +x \"{_ffmpegPath}\"\n\n" +
                       "Then try clicking Start again.";
            }
        }
    }

    public async Task CompressAsync(string input, string output, string targetFps, bool stripMetadata, int crf, string encoder, string selectedResolution, IProgress<ConversionProgress>? progress = null)
    {

        if (encoder == "copy")
        {          
            var copyArgs = $"-y -i \"{input}\" -c copy -map 0 \"{output}\"";
            await RunFfmpegProcessAsync(copyArgs, progress);
            return;
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

        if (encoder.Contains("nvenc"))
        {
            codecArgs = $"-vcodec {encoder} -preset p5 -rc vbr -cq {crf}";
        }
        else if (encoder.Contains("amf"))
        {
            codecArgs = $"-vcodec {encoder} -rc vbr_peak -qp_i {crf} -qp_p {crf} -quality quality";
        }
        else if (encoder.Contains("qsv"))
        {
            codecArgs = $"-vcodec {encoder} -preset veryfast -global_quality {crf}";
        }
        else
        {
            // Standard CPU (x265)
            codecArgs = $"-vcodec libx265 -crf {crf}";
        }

        string filterArgs = filters.Count > 0 ? $"-vf \"{string.Join(",", filters)}\"" : "";
        var args = $"-y -i \"{input}\" {filterArgs} {codecArgs} {metadataFlag} \"{output}\"";
        await RunFfmpegProcessAsync(args, progress);
    }

    // Smart target size  
    public async Task CompressTargetSizeAsync(string input, string output, string targetFps, bool stripMetadata, int targetMb, string encoder, string selectedResolution, double duration, IProgress<ConversionProgress>? progress = null)
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
        var pass1 = $"-y -i \"{input}\" {filterArgs} -c:v {encoder} -b:v {videoBitrate}k -pass 1 -passlogfile \"{logName}\" -an -f null {nullDev}";
        // PASS 2
        var pass2 = $"-y -i \"{input}\" {filterArgs} -c:v {encoder} -b:v {videoBitrate}k -pass 2 -passlogfile \"{logName}\" -c:a aac -b:a 128k \"{output}\"";

        await RunFfmpegProcessAsync(pass1, p1);
        await RunFfmpegProcessAsync(pass2, p2);

        if (File.Exists($"{logName}-0.log")) File.Delete($"{logName}-0.log");
        if (File.Exists($"{logName}-0.log.mbtree")) File.Delete($"{logName}-0.log.mbtree");
        if (File.Exists($"{logName}.log")) File.Delete($"{logName}.log");
    }

    private async Task RunFfmpegProcessAsync(string args, IProgress<ConversionProgress>? progress)
    {
        if (!File.Exists(_ffmpegPath)) throw new FileNotFoundException("FFmpeg not found", _ffmpegPath);

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
                        totalDuration = (double.Parse(match.Groups[1].Value) * 3600) + (double.Parse(match.Groups[2].Value) * 60) + double.Parse(match.Groups[3].Value);
                }

                var timeMatch = Regex.Match(line, @"time=(\d+):(\d+):(\d+\.\d+)");
                var speedMatch = Regex.Match(line, @"speed=\s*(\d+\.\d+x)");
                var fpsMatch = Regex.Match(line, @"fps=\s*(\d+)");

                if (timeMatch.Success && totalDuration > 0 && progress != null)
                {
                    double currentSeconds = (double.Parse(timeMatch.Groups[1].Value) * 3600) + (double.Parse(timeMatch.Groups[2].Value) * 60) + double.Parse(timeMatch.Groups[3].Value);
                    progress.Report( new ConversionProgress
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

        _currentProcess = null;
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
        catch { /* Process might have already closed */ }
    }

}

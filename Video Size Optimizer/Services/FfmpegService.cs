using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

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

    public async Task CompressAsync(string input, string output, int crf, string encoder, string selectedResolution, IProgress<double>? progress = null)
    {
        if (!File.Exists(_ffmpegPath))
            throw new FileNotFoundException("FFmpeg not found", _ffmpegPath);

        string scaleFilter = "";
        if (!string.IsNullOrEmpty(selectedResolution) && selectedResolution != "Original")
        {
            // Extract the width (e.g., from "1920 (1080p)")
            string width = selectedResolution.Split(' ')[0];
            scaleFilter = $"-vf scale={width}:-2";
        }

        string codecArgs;

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
        var args = $"-y -i \"{input}\" {scaleFilter} {codecArgs} \"{output}\"";


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

        var errorBuilder = new System.Text.StringBuilder();
        double totalDurationSeconds = 0;

        _currentProcess.Start();

        // Read the stream
        using (var reader = _currentProcess.StandardError)
        {
            while (await reader.ReadLineAsync() is string line)
            {
                
                errorBuilder.AppendLine(line);

                // 1. Get total duration
                if (totalDurationSeconds == 0)
                {
                    var match = Regex.Match(line, @"Duration:\s(\d+):(\d+):(\d+\.\d+)");
                    if (match.Success)
                    {
                        totalDurationSeconds = (double.Parse(match.Groups[1].Value) * 3600) +
                                               (double.Parse(match.Groups[2].Value) * 60) +
                                                double.Parse(match.Groups[3].Value);
                    }
                }

                // 2. Get current time
                var timeMatch = Regex.Match(line, @"time=(\d+):(\d+):(\d+\.\d+)");
                if (timeMatch.Success && totalDurationSeconds > 0)
                {
                    double currentSeconds = (double.Parse(timeMatch.Groups[1].Value) * 3600) +
                                            (double.Parse(timeMatch.Groups[2].Value) * 60) +
                                             double.Parse(timeMatch.Groups[3].Value);

                    double percentage = (currentSeconds / totalDurationSeconds) * 100;
                    progress?.Report(Math.Clamp(percentage, 0, 100));
                }
            }
        }

        await _currentProcess.WaitForExitAsync();

        // 3. CHECK FOR FAILURE
        int exitCode = _currentProcess.ExitCode;
        _currentProcess = null; 

        if (exitCode != 0)
        {
            throw new Exception($"FFmpeg failed with exit code {exitCode}.");
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
        catch { /* Process might have already closed */ }
    }

}

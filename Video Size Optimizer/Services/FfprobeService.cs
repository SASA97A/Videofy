using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Video_Size_Optimizer.Services
{
    public class FfprobeService
    {
        private readonly string _ffprobePath;
        private Process? _currentProcess;
        private static bool _initialized = false;
        private static readonly object _lock = new();

        public FfprobeService()
        {     
            _ffprobePath = AppPathService.FfprobeExecutable;
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
                        using var process = Process.Start("chmod", $"+x \"{_ffprobePath}\"");
                        process?.WaitForExit();
                    }

                    if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    {
                        var xattrArgs = $"-dr com.apple.quarantine \"{_ffprobePath}\"";
                        using var xattrProcess = Process.Start("xattr", xattrArgs);
                        xattrProcess?.WaitForExit();
                    }

                    _initialized = true;
                    return null;
                }
                catch (Exception ex)
                {
                    LogService.Instance.Log(
                        $"Permission initialization failed. Path={_ffprobePath} | {ex.Message}", LogLevel.Error, "FFPROBE");

                    return "Videofy doesn't have permission to analyze your videos.\n\n" +
                           "The FFprobe binary needs execution rights to work.\n\n" +
                           "How to fix:\n" +
                           "1. Open your Terminal\n" +
                           $"2. Run: chmod +x \"{_ffprobePath}\"\n\n" +
                           "Then try clicking Start again.";
                }
            }
        }

        public async Task<double> GetVideoDurationAsync(string filePath)
        {
            // ffprobe command to get duration in seconds
            var args = $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{filePath}\"";

            var startInfo = new ProcessStartInfo
            {
                FileName = _ffprobePath, 
                Arguments = args,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            try
            {
                using var process = Process.Start(startInfo);
                if (process == null) return 0;

                string output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                //return double.TryParse(output, out double seconds) ? seconds : 0;
                return double.TryParse(output, NumberStyles.Any, CultureInfo.InvariantCulture, out double seconds)
                        ? seconds
                        : 0;
            }
            catch (Exception ex)
            {
                LogService.Instance.Log(
                    $"Failed to read duration. File={filePath} | {ex.Message}", LogLevel.Error, "FFPROBE");
                return 0;
            }
        }


        public async Task<int> GetVideoWidthAsync(string inputPath)
        {
            if (!File.Exists(_ffprobePath)) return 0;

            _currentProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _ffprobePath,
                    // Arguments to get only the width of the first video stream in a clean format
                    Arguments = $"-v error -select_streams v:0 -show_entries stream=width -of csv=p=0 \"{inputPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true
                }
            };

            try
            {
                _currentProcess.Start();
                string output = await _currentProcess.StandardOutput.ReadToEndAsync();
                await _currentProcess.WaitForExitAsync();

                if (int.TryParse(output.Trim(), out int width))
                {
                    return width;
                }
            }
            catch (Exception ex)
            {
                LogService.Instance.Log(
                    $"Failed to read video width. File={inputPath} | {ex.Message}", LogLevel.Error, "FFPROBE");
                return 0;
            }
            finally
            {
                _currentProcess = null;
            }

            return 0;
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
                    $"Failed to kill ffprobe process | {ex.Message}", LogLevel.Error, "FFPROBE");
            }
        }
    }
}

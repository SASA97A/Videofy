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
                    $"Failed to read duration. File={filePath} | Error: {ex.Message}", LogLevel.Error, "FFPROBE");
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
                    $"Failed to read video width. File={inputPath} | Error: {ex.Message}", LogLevel.Error, "FFPROBE");
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

        public async Task<VideoMetadata> GetVideoMetadataAsync(string inputPath)
        {
            var meta = new VideoMetadata { Path = inputPath };
            if (!File.Exists(_ffprobePath)) return meta;

            var args = $"-v quiet -print_format json -show_format -show_streams \"{inputPath}\"";
            var startInfo = new ProcessStartInfo
            {
                FileName = _ffprobePath,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true
            };

            try
            {
                using var process = Process.Start(startInfo);
                if (process == null) return meta;

                string jsonOutput = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                using var doc = System.Text.Json.JsonDocument.Parse(jsonOutput);
                var root = doc.RootElement;

                if (root.TryGetProperty("format", out var formatEl))
                {
                    if (formatEl.TryGetProperty("duration", out var durEl) &&
                        double.TryParse(durEl.GetString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double duration))
                    {
                        meta.Duration = duration;
                    }
                }

                if (root.TryGetProperty("streams", out var streamsEl))
                {
                    foreach (var stream in streamsEl.EnumerateArray())
                    {
                        string codecType = stream.TryGetProperty("codec_type", out var ct) ? ct.GetString() ?? "" : "";
                        if (codecType == "video" && string.IsNullOrEmpty(meta.Video.Codec))
                        {
                            meta.Video.Codec = stream.TryGetProperty("codec_name", out var cn) ? cn.GetString() ?? "" : "";
                            meta.Video.Width = stream.TryGetProperty("width", out var w) ? w.GetInt32() : 0;
                            meta.Video.Height = stream.TryGetProperty("height", out var h) ? h.GetInt32() : 0;
                            meta.Video.PixFmt = stream.TryGetProperty("pix_fmt", out var pf) ? pf.GetString() ?? "" : "";

                            string fpsStr = stream.TryGetProperty("r_frame_rate", out var rfr) ? rfr.GetString() ?? "30/1" : "30/1";
                            var parts = fpsStr.Split('/');
                            if (parts.Length == 2 && double.TryParse(parts[0], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double num) &&
                                double.TryParse(parts[1], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double den) && den > 0)
                            {
                                meta.Video.Fps = num / den;
                            }
                            else
                            {
                                meta.Video.Fps = 30.0;
                            }
                        }
                        else if (codecType == "audio" && !meta.Audio.Exists)
                        {
                            meta.Audio.Exists = true;
                            meta.Audio.Codec = stream.TryGetProperty("codec_name", out var acn) ? acn.GetString() ?? "" : "";
                            if (stream.TryGetProperty("sample_rate", out var sr) && int.TryParse(sr.GetString(), out int srVal))
                                meta.Audio.SampleRate = srVal;
                            if (stream.TryGetProperty("channels", out var ch))
                                meta.Audio.Channels = ch.GetInt32();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogService.Instance.Log($"Failed to probe video metadata for {inputPath}: {ex.Message}", LogLevel.Error, "FFPROBE");
            }

            return meta;
        }
    }

    public class VideoStreamMetadata
    {
        public string Codec { get; set; } = "";
        public int Width { get; set; }
        public int Height { get; set; }
        public double Fps { get; set; }
        public string PixFmt { get; set; } = "";
    }

    public class AudioStreamMetadata
    {
        public bool Exists { get; set; }
        public string Codec { get; set; } = "";
        public int SampleRate { get; set; } = 48000;
        public int Channels { get; set; } = 2;
    }

    public class VideoMetadata
    {
        public string Path { get; set; } = "";
        public double Duration { get; set; }
        public VideoStreamMetadata Video { get; set; } = new();
        public AudioStreamMetadata Audio { get; set; } = new();
    }
}

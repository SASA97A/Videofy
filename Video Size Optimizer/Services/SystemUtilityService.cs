using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Video_Size_Optimizer.Services;

namespace Video_Size_Optimizer
{

    public enum AppLink
    {
        GitHub,
        BtbNReleases
    }

    public class SystemUtilityService
    {
        private readonly HttpClient _httpClient;
        private const string gitHubRepoUrl = "https://github.com/SASA97A/Videofy/releases";
        private const string btbNRepo = "https://github.com/BtbN/FFmpeg-Builds/releases";


        // GitHub API classes to parse the response
        public class GitHubRelease
        {
            [JsonPropertyName("assets")] public List<GitHubAsset> Assets { get; set; } = new();
        }
        public class GitHubAsset
        {
            [JsonPropertyName("name")] public string Name { get; set; } = "";
            [JsonPropertyName("browser_download_url")] public string DownloadUrl { get; set; } = "";
        }


        //Windows API for sleep preventation
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern uint SetThreadExecutionState(uint esFlags);
        private const uint ES_CONTINUOUS = 0x80000000;
        private const uint ES_SYSTEM_REQUIRED = 0x00000001;
        private const uint ES_AWAYMODE_REQUIRED = 0x00000040;

        // For linux sleep preventation
        private Process? _linuxInhibitProcess;

        public SystemUtilityService()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Videofy-App");
        }

        public void OpenAppWebLink(AppLink link)
        {
            LogService.Instance.Log($"Redirecting to Videofy page on Github.", LogLevel.Info, "SysUtil");
            string url = link switch
            {
                AppLink.GitHub => gitHubRepoUrl,
                AppLink.BtbNReleases => btbNRepo,               
                _ => gitHubRepoUrl
            };

            OpenExternalLink(url);
        }

        public void OpenExternalLink(string url)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                Process.Start("xdg-open", url);
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                Process.Start("open", url);
        }

        public void OpenLocalFolder(string path)
        {
            if (string.IsNullOrEmpty(path)) return;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                Process.Start("explorer.exe", path);
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                Process.Start("open", path);
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                Process.Start("xdg-open", path);
        }

        public async Task<string?> GetLatestGithubTagNameAsync(string repoOwner, string repoName)
        {
            try
            {
                var response = await _httpClient.GetStringAsync($"https://api.github.com/repos/{repoOwner}/{repoName}/releases/latest");
                using var doc = System.Text.Json.JsonDocument.Parse(response);
                return doc.RootElement.GetProperty("tag_name").GetString();
            }
            catch (Exception ex)
            {
                LogService.Instance.Log($"Connection failed: {ex.Message}.", LogLevel.Error, "SysUtil");
                return null;
            }
        }

        public async Task<bool> PreventSleepAsync(bool prevent, Func<string, string, Task>? showError = null)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (prevent)
                    SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED | ES_AWAYMODE_REQUIRED);
                else
                    SetThreadExecutionState(ES_CONTINUOUS);
                return true;
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // Never leave an older inhibit running, also when prevent is requested twice.
                StopLinuxInhibit();
                if (!prevent) return true;

                string? error;
                try
                {
                    // The inhibit lasts as long as its child command runs. "tail --pid" ends when Videofy
                    // ends, so the inhibit is released even if the app crashes or is killed (kill -9).
                    // With the old "sleep infinity" the inhibit stayed until reboot in that case.
                    var psi = new ProcessStartInfo
                    {
                        FileName = "systemd-inhibit",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };
                    psi.ArgumentList.Add("--what=idle:sleep");
                    psi.ArgumentList.Add("--who=Videofy");
                    psi.ArgumentList.Add("--why=Encoding Video");
                    psi.ArgumentList.Add("tail");
                    psi.ArgumentList.Add($"--pid={Environment.ProcessId}");
                    psi.ArgumentList.Add("-f");
                    psi.ArgumentList.Add("/dev/null");

                    var process = Process.Start(psi);
                    if (process == null)
                    {
                        error = "systemd-inhibit could not be started.";
                    }
                    else
                    {
                        // Process.Start succeeds even when systemd-inhibit is refused (for example no
                        // logind session: "Failed to inhibit: Access denied"). It then exits at once.
                        await Task.Delay(300);
                        if (!process.HasExited)
                        {
                            _linuxInhibitProcess = process;
                            LogService.Instance.Log("System sleep prevention enabled (systemd-inhibit).", LogLevel.Info, "SysUtil");
                            return true;
                        }

                        string stderr = (await process.StandardError.ReadToEndAsync()).Trim();
                        error = $"systemd-inhibit exited with code {process.ExitCode}" +
                                (stderr.Length > 0 ? $": {stderr}" : ".");
                        process.Dispose();
                    }
                }
                catch (System.ComponentModel.Win32Exception)
                {
                    error = "'systemd-inhibit' was not found on your system.";
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                }

                LogService.Instance.Log($"Sleep prevention failed: {error}", LogLevel.Warning, "SysUtil");
                if (showError != null)
                {
                    await showError("Linux System Limit",
                        $"Could not prevent sleep mode.\n\n{error}\n\n" +
                        "Please ensure your system power settings allow long-running tasks.");
                }
                return false;
            }
            return true;
        }

        private void StopLinuxInhibit()
        {
            var process = _linuxInhibitProcess;
            _linuxInhibitProcess = null;
            if (process == null) return;

            try
            {
                // Kill(true) also ends the child (tail), not only systemd-inhibit.
                if (!process.HasExited) process.Kill(true);
            }
            catch (Exception ex)
            {
                LogService.Instance.Log($"Failed to stop systemd-inhibit: {ex.Message}", LogLevel.Warning, "SysUtil");
            }
            finally
            {
                process.Dispose();
            }
        }

        public async Task InstallFfmpegAsync(string destinationFolder, IProgress<string> statusReporter)
        {
            bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            bool isLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

            if (!isWindows && !isLinux)
                throw new PlatformNotSupportedException("Auto-download is only supported on Windows and Linux.");

            LogService.Instance.Section("FFmpeg Auto-Update");
            LogService.Instance.Log("Searching for latest FFmpeg builds...", LogLevel.Info, "SysUtil");
            statusReporter.Report("Searching for latest FFmpeg builds...");

            string downloadUrl;
            string archiveName;
            string remoteFileName;

            try
            {
                var release = await _httpClient.GetFromJsonAsync<GitHubRelease>("https://api.github.com/repos/BtbN/FFmpeg-Builds/releases/latest");

                if (release == null || !release.Assets.Any())
                    throw new Exception("Could not retrieve file list from GitHub.");

                // Filter for the "n8" GPL builds
                // Criteria: win64-gpl (Windows) or linux64-gpl (Linux) and matches 'n' versioning
                GitHubAsset? targetAsset;

                if (isWindows)
                {
                    targetAsset = release.Assets.FirstOrDefault(a =>
                        a.Name.Contains("win64-gpl") &&
                        a.Name.Contains("-n8") &&
                        a.Name.EndsWith(".zip"));
                    archiveName = "ffmpeg_setup.zip";
                }
                else
                {
                    targetAsset = release.Assets.FirstOrDefault(a =>
                        a.Name.Contains("linux64-gpl") &&
                        a.Name.Contains("-n8") &&
                        a.Name.EndsWith(".tar.xz"));
                    archiveName = "ffmpeg_setup.tar.xz";
                }

                if (targetAsset == null)
                    throw new Exception("Could not find a compatible GPL build in the latest release.");

                downloadUrl = targetAsset.DownloadUrl;
                remoteFileName = targetAsset.Name;

                LogService.Instance.Log($"Build Identified: {remoteFileName}", LogLevel.Success, "SysUtil");
                LogService.Instance.Log($"Direct Link: {downloadUrl}", LogLevel.Info, "SysUtil");

            }
            catch (Exception ex)
            {
                LogService.Instance.Log($"GitHub API Error: {ex.Message}", LogLevel.Error, "SysUtil");
                throw;
            }

            string tempFolder = Path.Combine(Path.GetTempPath(), "Videofy_Setup_" + Guid.NewGuid());
            Directory.CreateDirectory(tempFolder);
            string archivePath = Path.Combine(tempFolder, archiveName);

            string finalBinFolder = AppPathService.FfmpegBinFolder;
            AppPathService.EnsureDirectories();

            try
            {
                LogService.Instance.Log($"Downloading asset: {remoteFileName}...", LogLevel.Info, "SysUtil");
                statusReporter.Report($"Downloading {Path.GetFileName(downloadUrl)}...");
                using (var stream = await _httpClient.GetStreamAsync(downloadUrl))
                using (var fileStream = new FileStream(archivePath, FileMode.Create))
                {
                    await stream.CopyToAsync(fileStream);
                }

                LogService.Instance.Log("Extracting files...", LogLevel.Info, "SysUtil");
                statusReporter.Report("Extracting files...");
                if (isWindows)
                {
                    ZipFile.ExtractToDirectory(archivePath, tempFolder);
                }
                else
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "tar",
                        Arguments = $"-xf \"{archivePath}\" -C \"{tempFolder}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using var p = Process.Start(psi);
                    if (p != null) await p.WaitForExitAsync();
                }

                LogService.Instance.Log("Installing binaries...", LogLevel.Info, "SysUtil");
                statusReporter.Report("Installing...");
                string ffmpegExe = isWindows ? "ffmpeg.exe" : "ffmpeg";
                string ffprobeExe = isWindows ? "ffprobe.exe" : "ffprobe";

                var foundFfmpeg = Directory.GetFiles(tempFolder, ffmpegExe, SearchOption.AllDirectories).FirstOrDefault();
                var foundFfprobe = Directory.GetFiles(tempFolder, ffprobeExe, SearchOption.AllDirectories).FirstOrDefault();

                if (foundFfmpeg == null || foundFfprobe == null)
                    throw new FileNotFoundException("Could not locate ffmpeg/ffprobe inside the downloaded archive.");

                File.Move(foundFfmpeg, Path.Combine(finalBinFolder, ffmpegExe), true);
                File.Move(foundFfprobe, Path.Combine(finalBinFolder, ffprobeExe), true);

                if (!isWindows)
                {
                    Process.Start("chmod", $"+x \"{Path.Combine(finalBinFolder, ffmpegExe)}\"");
                    Process.Start("chmod", $"+x \"{Path.Combine(finalBinFolder, ffprobeExe)}\"");
                }
                LogService.Instance.Log("FFmpeg binaries successfully installed.", LogLevel.Success, "SysUtil");
            }
            catch (Exception ex)
            {
                LogService.Instance.Log($"Asset download or extraction failed: {ex.Message}", LogLevel.Error, "SysUtil");
                throw;
            }
            finally
            {
                if (Directory.Exists(tempFolder))
                    Directory.Delete(tempFolder, true);
            }
        }

        public async Task SendDesktopNotificationAsync(string title, string message)
        {
            await Task.Run(() =>
            {
                try
                {
                    LogService.Instance.Log($"Triggering OS notification: {title} - {message}", LogLevel.Info, "SysUtil");

                    bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
                    bool isMac = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
                    bool isLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

                    if (isWindows)
                    {
                        IntPtr hwnd = Dispatcher.UIThread.InvokeAsync(GetMainWindowHandle).GetAwaiter().GetResult();
                        WindowsNotifier.ShowAsync(hwnd, title, message).GetAwaiter().GetResult();
                    }
                    else if (isMac)
                    {
                        string cleanMsg = message.Replace("\"", "\\\"").Replace("\n", " ");
                        string cleanTitle = title.Replace("\"", "\\\"");
                        Process.Start("osascript", $"-e \"display notification \\\"{cleanMsg}\\\" with title \\\"{cleanTitle}\\\"\"");
                    }
                    else if (isLinux)
                    {
                        var psi = new ProcessStartInfo("notify-send") { UseShellExecute = false };
                        psi.ArgumentList.Add("--app-name=Videofy");
                        string? iconPath = GetLinuxNotificationIconPath();
                        if (iconPath != null) psi.ArgumentList.Add($"--icon={iconPath}");
                        psi.ArgumentList.Add(title);
                        psi.ArgumentList.Add(message);
                        Process.Start(psi);
                    }
                }
                catch (Exception ex)
                {
                    LogService.Instance.Log($"Desktop notification error: {ex.Message}", LogLevel.Error, "SysUtil");
                }
            });
        }

        // notify-send needs the icon as a file on disk, so copy the embedded app.png out once.
        private static string? GetLinuxNotificationIconPath()
        {
            try
            {
                string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Videofy");
                string iconPath = Path.Combine(folder, "app.png");
                if (!File.Exists(iconPath))
                {
                    Directory.CreateDirectory(folder);
                    using var source = AssetLoader.Open(new Uri("avares://Videofy/Assets/app.png"));
                    using var target = File.Create(iconPath);
                    source.CopyTo(target);
                }
                return iconPath;
            }
            catch (Exception ex)
            {
                LogService.Instance.Log($"Could not prepare notification icon: {ex.Message}", LogLevel.Warning, "SysUtil");
                return null;
            }
        }

        private static IntPtr GetMainWindowHandle()
        {
            var mainWindow = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            return mainWindow?.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        }

        public async Task PlayCompletionSoundAsync()
        {
            await Task.Run(() =>
            {
                try
                {
                    LogService.Instance.Log("Triggering completion audio chime...", LogLevel.Info, "SysUtil");

                    bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
                    bool isMac = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
                    bool isLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

                    if (isWindows)
                    {
                        string soundScript = "[System.Media.SystemSounds]::Asterisk.Play()";
                        byte[] soundBytes = System.Text.Encoding.Unicode.GetBytes(soundScript);
                        string base64Sound = Convert.ToBase64String(soundBytes);
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = "powershell.exe",
                            Arguments = $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -EncodedCommand {base64Sound}",
                            UseShellExecute = true,
                            WindowStyle = ProcessWindowStyle.Hidden
                        });
                    }
                    else if (isMac)
                    {
                        Process.Start("afplay", "/System/Library/Sounds/Glass.aiff");
                    }
                    else if (isLinux)
                    {
                        Process.Start("paplay", "/usr/share/sounds/freedesktop/stereo/complete.oga");
                    }
                }
                catch (Exception ex)
                {
                    LogService.Instance.Log($"Audio chime error: {ex.Message}", LogLevel.Error, "SysUtil");
                }
            });
        }
    }
}

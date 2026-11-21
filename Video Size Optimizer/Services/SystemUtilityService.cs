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
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
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
        // The BtbN/FFmpeg-Builds repository moved to FFmpeg-Builds/FFmpeg-Builds; the old address only redirects.
        private const string btbNRepo = "https://github.com/FFmpeg-Builds/FFmpeg-Builds/releases";
        private const string ffmpegBuildsApiLatest = "https://api.github.com/repos/FFmpeg-Builds/FFmpeg-Builds/releases/latest";


        // GitHub API classes to parse the response
        public class GitHubRelease
        {
            [JsonPropertyName("assets")] public List<GitHubAsset> Assets { get; set; } = new();
        }
        public class GitHubAsset
        {
            [JsonPropertyName("name")] public string Name { get; set; } = "";
            [JsonPropertyName("browser_download_url")] public string DownloadUrl { get; set; } = "";
            // "sha256:<hex>", published by GitHub for each release file.
            [JsonPropertyName("digest")] public string? Digest { get; set; }
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

            GitHubAsset targetAsset;
            try
            {
                var release = await _httpClient.GetFromJsonAsync<GitHubRelease>(ffmpegBuildsApiLatest);

                if (release == null || !release.Assets.Any())
                    throw new Exception("Could not retrieve file list from GitHub.");

                targetAsset = PickFfmpegAsset(release.Assets, isWindows)
                    ?? throw new Exception("Could not find a compatible GPL build in the latest release.");

                LogService.Instance.Log($"Build Identified: {targetAsset.Name}", LogLevel.Success, "SysUtil");
                LogService.Instance.Log($"Direct Link: {targetAsset.DownloadUrl}", LogLevel.Info, "SysUtil");
            }
            catch (Exception ex)
            {
                LogService.Instance.Log($"GitHub API Error: {ex.Message}", LogLevel.Error, "SysUtil");
                throw;
            }

            string archiveName = isWindows ? "ffmpeg_setup.zip" : "ffmpeg_setup.tar.xz";
            string tempFolder = Path.Combine(Path.GetTempPath(), "Videofy_Setup_" + Guid.NewGuid());
            Directory.CreateDirectory(tempFolder);
            string archivePath = Path.Combine(tempFolder, archiveName);
            string extractFolder = Path.Combine(tempFolder, "extract");
            Directory.CreateDirectory(extractFolder);

            string finalBinFolder = AppPathService.FfmpegBinFolder;
            AppPathService.EnsureDirectories();

            string ffmpegExe = isWindows ? "ffmpeg.exe" : "ffmpeg";
            string ffprobeExe = isWindows ? "ffprobe.exe" : "ffprobe";
            string finalFfmpeg = Path.Combine(finalBinFolder, ffmpegExe);
            string finalFfprobe = Path.Combine(finalBinFolder, ffprobeExe);
            string stagedFfmpeg = finalFfmpeg + ".new";
            string stagedFfprobe = finalFfprobe + ".new";

            try
            {
                LogService.Instance.Log($"Downloading asset: {targetAsset.Name}...", LogLevel.Info, "SysUtil");
                statusReporter.Report($"Downloading {targetAsset.Name}...");
                using (var stream = await _httpClient.GetStreamAsync(targetAsset.DownloadUrl))
                using (var fileStream = new FileStream(archivePath, FileMode.Create))
                {
                    await stream.CopyToAsync(fileStream);
                }

                // GitHub publishes a SHA-256 for every release file. A broken or altered download stops here.
                statusReporter.Report("Verifying download...");
                await VerifyDigestAsync(archivePath, targetAsset.Digest);

                LogService.Instance.Log("Extracting files...", LogLevel.Info, "SysUtil");
                statusReporter.Report("Extracting files...");
                if (isWindows)
                {
                    // ExtractToDirectory refuses entries that would land outside the folder ("..\").
                    ZipFile.ExtractToDirectory(archivePath, extractFolder);
                }
                else
                {
                    await ExtractTarAsync(archivePath, extractFolder);
                }

                LogService.Instance.Log("Installing binaries...", LogLevel.Info, "SysUtil");
                statusReporter.Report("Installing...");

                var foundFfmpeg = Directory.GetFiles(extractFolder, ffmpegExe, SearchOption.AllDirectories).FirstOrDefault();
                var foundFfprobe = Directory.GetFiles(extractFolder, ffprobeExe, SearchOption.AllDirectories).FirstOrDefault();

                if (foundFfmpeg == null || foundFfprobe == null)
                    throw new FileNotFoundException("Could not locate ffmpeg/ffprobe inside the downloaded archive.");

                // Copy both next to the old files first, then swap. A failure before the swap leaves
                // the old ffmpeg and ffprobe untouched, never a new ffmpeg next to an old ffprobe.
                File.Copy(foundFfmpeg, stagedFfmpeg, true);
                File.Copy(foundFfprobe, stagedFfprobe, true);
                if (!OperatingSystem.IsWindows())
                {
                    const UnixFileMode executable =
                        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                        UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                        UnixFileMode.OtherRead | UnixFileMode.OtherExecute;
                    File.SetUnixFileMode(stagedFfmpeg, executable);
                    File.SetUnixFileMode(stagedFfprobe, executable);
                }
                File.Move(stagedFfmpeg, finalFfmpeg, true);
                File.Move(stagedFfprobe, finalFfprobe, true);

                LogService.Instance.Log("FFmpeg binaries successfully installed.", LogLevel.Success, "SysUtil");
            }
            catch (Exception ex)
            {
                LogService.Instance.Log($"Asset download or extraction failed: {ex.Message}", LogLevel.Error, "SysUtil");
                throw;
            }
            finally
            {
                foreach (var staged in new[] { stagedFfmpeg, stagedFfprobe })
                {
                    try { if (File.Exists(staged)) File.Delete(staged); } catch { }
                }
                try
                {
                    if (Directory.Exists(tempFolder))
                        Directory.Delete(tempFolder, true);
                }
                catch (Exception ex)
                {
                    LogService.Instance.Log($"Could not delete temp folder {tempFolder}: {ex.Message}", LogLevel.Warning, "SysUtil");
                }
            }
        }

        // Release builds are named like "ffmpeg-n9.0-latest-win64-gpl-9.0.zip". The full name is matched,
        // so the "-shared" (needs DLLs next to it) and "lgpl" builds are never picked.
        private static readonly Regex WindowsReleaseBuild =
            new(@"^ffmpeg-n(\d+(?:\.\d+)*)-latest-win64-gpl-\d+(?:\.\d+)*\.zip$", RegexOptions.IgnoreCase);
        private static readonly Regex LinuxReleaseBuild =
            new(@"^ffmpeg-n(\d+(?:\.\d+)*)-latest-linux64-gpl-\d+(?:\.\d+)*\.tar\.xz$", RegexOptions.IgnoreCase);

        // FFmpeg 9.0 hangs on some damaged recordings (no frames, 0% CPU) where 8.x encodes them in
        // seconds. Stay on 8.x until the app has been tested with a newer major version.
        public const int PreferredFfmpegMajor = 8;

        /// <summary>
        /// Picks the newest numbered GPL release build of <see cref="PreferredFfmpegMajor"/> (e.g. n8.1).
        /// If the release has none, the newest numbered build of any version, then the "master" GPL build.
        /// </summary>
        public static GitHubAsset? PickFfmpegAsset(IEnumerable<GitHubAsset> assets, bool isWindows)
        {
            var pattern = isWindows ? WindowsReleaseBuild : LinuxReleaseBuild;

            var numbered = assets
                .Select(a => (Asset: a, Match: pattern.Match(a.Name)))
                .Where(x => x.Match.Success)
                .Select(x => (x.Asset, Version: ParseBuildVersion(x.Match.Groups[1].Value)))
                .Where(x => x.Version != null)
                .OrderByDescending(x => x.Version)
                .ToList();

            var preferred = numbered.FirstOrDefault(x => x.Version!.Major == PreferredFfmpegMajor).Asset;
            if (preferred != null) return preferred;

            var newest = numbered.Select(x => x.Asset).FirstOrDefault();
            if (newest != null)
            {
                LogService.Instance.Log($"No FFmpeg {PreferredFfmpegMajor}.x build in the release; using {newest.Name}.", LogLevel.Warning, "SysUtil");
                return newest;
            }

            string master = isWindows ? "ffmpeg-master-latest-win64-gpl.zip" : "ffmpeg-master-latest-linux64-gpl.tar.xz";
            return assets.FirstOrDefault(a => string.Equals(a.Name, master, StringComparison.OrdinalIgnoreCase));
        }

        private static Version? ParseBuildVersion(string text)
        {
            // "9" alone is not a valid System.Version; "9.0" is.
            if (!text.Contains('.')) text += ".0";
            return Version.TryParse(text, out var version) ? version : null;
        }

        private static async Task VerifyDigestAsync(string filePath, string? digest)
        {
            const string prefix = "sha256:";
            if (string.IsNullOrEmpty(digest) || !digest.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                LogService.Instance.Log("No SHA-256 published for this file; download not verified.", LogLevel.Warning, "SysUtil");
                return;
            }

            string expected = digest.Substring(prefix.Length);
            string actual;
            using (var file = File.OpenRead(filePath))
            {
                actual = Convert.ToHexString(await SHA256.HashDataAsync(file));
            }

            if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
                throw new Exception($"Download is damaged: SHA-256 does not match (expected {expected}, got {actual}).");

            LogService.Instance.Log("Download verified (SHA-256).", LogLevel.Info, "SysUtil");
        }

        private static async Task ExtractTarAsync(string archivePath, string destination)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "tar",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };
            psi.ArgumentList.Add("-xf");
            psi.ArgumentList.Add(archivePath);
            psi.ArgumentList.Add("-C");
            psi.ArgumentList.Add(destination);

            Process? process;
            try
            {
                process = Process.Start(psi);
            }
            catch (System.ComponentModel.Win32Exception)
            {
                throw new Exception("'tar' was not found on your system. It is needed to unpack FFmpeg.");
            }
            if (process == null) throw new Exception("'tar' could not be started.");

            using (process)
            {
                var stdout = process.StandardOutput.ReadToEndAsync();
                string stderr = await process.StandardError.ReadToEndAsync();
                await stdout;
                await process.WaitForExitAsync();
                if (process.ExitCode != 0)
                    throw new Exception($"Unpacking failed (tar exit code {process.ExitCode}): {stderr.Trim()}");
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

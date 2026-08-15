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
                try
                {
                    if (prevent)
                    {
                        _linuxInhibitProcess = Process.Start(new ProcessStartInfo
                        {
                            FileName = "systemd-inhibit",
                            Arguments = "--what=idle:sleep --who=Videofy --why=\"Encoding Video\" sleep infinity",
                            UseShellExecute = false,
                            CreateNoWindow = true
                        });
                    }
                    else
                    {
                        _linuxInhibitProcess?.Kill();
                        _linuxInhibitProcess = null;
                    }
                    return true;
                }
                catch
                {
                    if (prevent && showError != null)
                    {
                        await showError("Linux System Limit",
                            "Could not prevent sleep mode. 'systemd-inhibit' was not found on your system. " +
                            "Please ensure your system power settings allow long-running tasks.");
                    }
                    return false;
                }
            }
            return true;
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
                        string cleanTitle = title.Replace("'", "''");
                        string cleanMsg = message.Replace("'", "''").Replace("\r\n", " ").Replace("\n", " ");
                        string currentExe = Process.GetCurrentProcess().MainModule?.FileName?.Replace("'", "''") ?? "";
                        string currentDir = AppDomain.CurrentDomain.BaseDirectory.Replace("'", "''");

                        string psScript = $@"
$shortcutPath = ""$env:APPDATA\Microsoft\Windows\Start Menu\Programs\Videofy.lnk""
try {{
    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($shortcutPath)
    $shortcut.TargetPath = '{currentExe}'
    $shortcut.WorkingDirectory = '{currentDir}'
    $shortcut.Description = 'Videofy Video Size Optimizer'
    $shortcut.Save()
}} catch {{ }}

try {{
    [Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType = WindowsRuntime] | Out-Null
    [Windows.Data.Xml.Dom.XmlDocument, Windows.Data.Xml.Dom.XmlDocument, ContentType = WindowsRuntime] | Out-Null
    $xml = New-Object Windows.Data.Xml.Dom.XmlDocument
    $xml.LoadXml('<toast><visual><binding template=""ToastGeneric""><text>{cleanTitle}</text><text>{cleanMsg}</text></binding></visual></toast>')
    $toast = [Windows.UI.Notifications.ToastNotification]::new($xml)
    [Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier('Videofy').Show($toast)
}} catch {{ }}

try {{
    Add-Type -AssemblyName System.Windows.Forms
    Add-Type -AssemblyName System.Drawing
    $icon = [System.Drawing.SystemIcons]::Information
    $notify = New-Object System.Windows.Forms.NotifyIcon
    $notify.Icon = $icon
    $notify.Visible = $true
    $notify.BalloonTipTitle = '{cleanTitle}'
    $notify.BalloonTipText = '{cleanMsg}'
    $notify.BalloonTipIcon = [System.Windows.Forms.ToolTipIcon]::Info
    $notify.ShowBalloonTip(5000)

    $timer = New-Object System.Windows.Forms.Timer
    $timer.Interval = 4000
    $timer.add_Tick({{
        $notify.Visible = $false
        $notify.Dispose()
        [System.Windows.Forms.Application]::Exit()
    }})
    $timer.Start()
    [System.Windows.Forms.Application]::Run()
}} catch {{ }}
";
                        byte[] scriptBytes = System.Text.Encoding.Unicode.GetBytes(psScript);
                        string base64Script = Convert.ToBase64String(scriptBytes);

                        Process.Start(new ProcessStartInfo
                        {
                            FileName = "powershell.exe",
                            Arguments = $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -EncodedCommand {base64Script}",
                            UseShellExecute = true,
                            WindowStyle = ProcessWindowStyle.Hidden
                        });
                    }
                    else if (isMac)
                    {
                        string cleanMsg = message.Replace("\"", "\\\"").Replace("\n", " ");
                        string cleanTitle = title.Replace("\"", "\\\"");
                        Process.Start("osascript", $"-e \"display notification \\\"{cleanMsg}\\\" with title \\\"{cleanTitle}\\\"\"");
                    }
                    else if (isLinux)
                    {
                        string cleanMsg = message.Replace("\"", "\\\"").Replace("\n", " ");
                        string cleanTitle = title.Replace("\"", "\\\"");
                        Process.Start("notify-send", $"\"{cleanTitle}\" \"{cleanMsg}\"");
                    }
                }
                catch (Exception ex)
                {
                    LogService.Instance.Log($"Desktop notification error: {ex.Message}", LogLevel.Error, "SysUtil");
                }
            });
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

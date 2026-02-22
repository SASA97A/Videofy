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
                LogService.Instance.Log($"Connection failed {ex.Message}.", LogLevel.Error, "SysUtil");
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
                        (a.Name.Contains("-n") || a.Name.Contains("master")) &&
                        a.Name.EndsWith(".tar.xz"));
                    archiveName = "ffmpeg_setup.tar.xz";
                }

                if (targetAsset == null)
                    throw new Exception("Could not find a compatible GPL build in the latest release.");

                downloadUrl = targetAsset.DownloadUrl;
                remoteFileName = targetAsset.Name;


                LogService.Instance.Section("FFmpeg Update Found");
                LogService.Instance.Log($"Build Identified: {remoteFileName}", LogLevel.Success);
                LogService.Instance.Log($"Direct Link: {downloadUrl}", LogLevel.Info);

            }
            catch (Exception ex)
            {
                LogService.Instance.Log($"GitHub API Error: {ex.Message}", LogLevel.Error, "Error");
                throw;
            }

            string tempFolder = Path.Combine(Path.GetTempPath(), "Videofy_Setup_" + Guid.NewGuid());
            Directory.CreateDirectory(tempFolder);
            string archivePath = Path.Combine(tempFolder, archiveName);

            string finalBinFolder = AppPathService.FfmpegBinFolder;
            AppPathService.EnsureDirectories();

            try
            {
                statusReporter.Report($"Downloading {Path.GetFileName(downloadUrl)}...");
                using (var stream = await _httpClient.GetStreamAsync(downloadUrl))
                using (var fileStream = new FileStream(archivePath, FileMode.Create))
                {
                    await stream.CopyToAsync(fileStream);
                }

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
            }
            finally
            {
                if (Directory.Exists(tempFolder))
                    Directory.Delete(tempFolder, true);
            }
        }

    }
}

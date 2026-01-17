using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Video_Size_Optimizer
{

    public enum AppLink
    {
        GitHub,
        BtbNReleases,
        WinUrl,
        LinuxUrl
    }

    public class SystemUtilityService
    {
        private readonly HttpClient _httpClient;
        private const string gitHubRepoUrl = "https://github.com/SASA97A/Videofy/releases";
        private const string btbNRepo = "https://github.com/BtbN/FFmpeg-Builds/releases";
        private const string WinUrl = "https://github.com/BtbN/FFmpeg-Builds/releases/download/autobuild-2026-01-16-12-57/ffmpeg-n8.0.1-48-g0592be14ff-win64-gpl-8.0.zip";
        private const string LinuxUrl = "https://github.com/BtbN/FFmpeg-Builds/releases/download/autobuild-2026-01-16-12-57/ffmpeg-n8.0.1-48-g0592be14ff-linux64-gpl-8.0.tar.xz";
        public SystemUtilityService()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Videofy-App");
        }

        public void OpenAppWebLink(AppLink link)
        {
            string url = link switch
            {
                AppLink.GitHub => gitHubRepoUrl,
                AppLink.BtbNReleases => btbNRepo,
                AppLink.WinUrl => WinUrl,
                AppLink.LinuxUrl => LinuxUrl,
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
            catch
            {
                return null;
            }
        }

        public async Task InstallFfmpegAsync(string destinationFolder, IProgress<string> statusReporter)
        {
            string downloadUrl;
            string archiveName;
            bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

            if (isWindows)
            {
                downloadUrl = WinUrl;
                archiveName = "ffmpeg_setup.zip";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                downloadUrl = LinuxUrl;
                archiveName = "ffmpeg_setup.tar.xz";
            }
            else
            {
                throw new PlatformNotSupportedException("Auto-download is currently only supported on Windows and Linux.");
            }

            string tempFolder = Path.Combine(Path.GetTempPath(), "Videofy_Setup_" + Guid.NewGuid());
            Directory.CreateDirectory(tempFolder);
            string archivePath = Path.Combine(tempFolder, archiveName);

            try
            {
                // 1. Download
                statusReporter.Report("Downloading FFmpeg binaries...");
                using (var stream = await _httpClient.GetStreamAsync(downloadUrl))
                using (var fileStream = new FileStream(archivePath, FileMode.Create))
                {
                    await stream.CopyToAsync(fileStream);
                }

                // 2. Extract
                statusReporter.Report("Extracting files...");
                if (isWindows)
                {
                    ZipFile.ExtractToDirectory(archivePath, tempFolder);
                }
                else
                {
                    // Linux: Use native tar command for .tar.xz
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

                // 3. Recursive Search & Install
                statusReporter.Report("Installing...");

                string ffmpegExe = isWindows ? "ffmpeg.exe" : "ffmpeg";
                string ffprobeExe = isWindows ? "ffprobe.exe" : "ffprobe";

                // Find the files anywhere inside the temp folder (e.g. inside bin/)
                var foundFfmpeg = Directory.GetFiles(tempFolder, ffmpegExe, SearchOption.AllDirectories).FirstOrDefault();
                var foundFfprobe = Directory.GetFiles(tempFolder, ffprobeExe, SearchOption.AllDirectories).FirstOrDefault();

                if (foundFfmpeg == null || foundFfprobe == null)
                {
                    throw new FileNotFoundException("Could not locate ffmpeg/ffprobe inside the downloaded archive.");
                }

                // Create target directory if missing
                if (!Directory.Exists(destinationFolder))
                    Directory.CreateDirectory(destinationFolder);

                // Move files to final destination (Overwrite if exists)
                File.Move(foundFfmpeg, Path.Combine(destinationFolder, ffmpegExe), true);
                File.Move(foundFfprobe, Path.Combine(destinationFolder, ffprobeExe), true);

                // Linux: Ensure +x permission
                if (!isWindows)
                {
                    Process.Start("chmod", $"+x \"{Path.Combine(destinationFolder, ffmpegExe)}\"");
                    Process.Start("chmod", $"+x \"{Path.Combine(destinationFolder, ffprobeExe)}\"");
                }
            }
            finally
            {
                // 4. Cleanup
                if (Directory.Exists(tempFolder))
                    Directory.Delete(tempFolder, true);
            }
        }

    }
}

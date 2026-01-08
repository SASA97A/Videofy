using System;
using System.IO;
using System.Runtime.InteropServices;


namespace Video_Size_Optimizer
{
    public static class DependencyChecker
    {
        public static bool CheckBinaries(out string missingPath)
        {
            var baseDir = AppContext.BaseDirectory;
            string subDir = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "win-x64" :
                            RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "osx-x64" : "linux-x64";

            string ffmpegName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "ffmpeg.exe" : "ffmpeg";
            string ffprobeName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "ffprobe.exe" : "ffprobe";

            string ffmpegPath = Path.Combine(baseDir, "ffmpeg", subDir, ffmpegName);
            string ffprobePath = Path.Combine(baseDir, "ffmpeg", subDir, ffprobeName);

            // Get just the folder path to show the user
            missingPath = Path.GetDirectoryName(ffmpegPath) ?? "Unknown Path";

            if (!File.Exists(ffmpegPath)) return false;
            if (!File.Exists(ffprobePath)) return false;

            return true;
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Video_Size_Optimizer.Services;

public static class AppPathService
{
    public static readonly string AppDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Videofy"
        );

    // Subfolder for binaries
    public static string FfmpegBinFolder => Path.Combine(AppDataFolder, "bin");

    public static string FfmpegExecutable => Path.Combine(FfmpegBinFolder,
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "ffmpeg.exe" : "ffmpeg");

    public static string FfprobeExecutable => Path.Combine(FfmpegBinFolder,
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "ffprobe.exe" : "ffprobe");

    public static void EnsureDirectories()
    {
        if (!Directory.Exists(AppDataFolder)) Directory.CreateDirectory(AppDataFolder);
        if (!Directory.Exists(FfmpegBinFolder)) Directory.CreateDirectory(FfmpegBinFolder);
    }
}

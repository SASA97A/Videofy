using System;
using System.IO;
using System.Runtime.InteropServices;
using Video_Size_Optimizer.Services;


namespace Video_Size_Optimizer
{
    public static class DependencyChecker
    {
        public static bool CheckBinaries(out string missingPath)
        {
            AppPathService.EnsureDirectories();

            missingPath = AppPathService.FfmpegBinFolder;

            if (!File.Exists(AppPathService.FfmpegExecutable)) return false;
            if (!File.Exists(AppPathService.FfprobeExecutable)) return false;

            return true;
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Video_Size_Optimizer.Utils;

namespace Video_Size_Optimizer.Services;



public class FileService
{
    public (long totalSize, List<string> videoPaths) GetFolderData(string folder)
    {
        long totalSize = 0;
        var videoPaths = new List<string>();

        var allFiles = Directory.EnumerateFiles(folder, "*.*", SearchOption.AllDirectories);

        foreach (var file in allFiles)
        {
            var info = new FileInfo(file);
            totalSize += info.Length;

            if (AppConstants.SupportedInputExtensions.Contains(info.Extension.ToLower()))
            {
                videoPaths.Add(file);
            }
        }

        return (totalSize, videoPaths);
    }

    //public string SanitizeFileName(string input)
    //{
    //    if (string.IsNullOrEmpty(input)) return input;
    //    var invalidChars = Path.GetInvalidFileNameChars();
    //    return new string(input.Where(c => !invalidChars.Contains(c)).ToArray());
    //}

    public bool IsPathLengthValid(string directory, string fileName, string extension)
    {
        // Safety buffer for Windows (Max 260)
        return (directory.Length + fileName.Length + extension.Length) < 250;
    }

    public string GenerateOutputPath(string inputPath, int crfValue, string extension)
    {
        return BuildFinalPath(inputPath, $"-CRF{crfValue}", extension);
    }

    public string GenerateTargetSizePath(string inputPath, int targetMb, string extension)
    {
        return BuildFinalPath(inputPath, $"-Target{targetMb}MB", extension);
    }

    private string BuildFinalPath(string inputPath, string suffix, string extension = ".mp4")
    {
        string directory = Path.GetDirectoryName(inputPath) ?? "";
        string fileNameOnly = Path.GetFileNameWithoutExtension(inputPath);
        
        string candidatePath = Path.Combine(directory, $"{fileNameOnly}{suffix}{extension}");

        return GetUniqueFilePath(candidatePath);
    }

    public string GetUniqueFilePath(string filePath)
    {
        if (!File.Exists(filePath)) return filePath;

        string directory = Path.GetDirectoryName(filePath) ?? "";
        string fileName = Path.GetFileNameWithoutExtension(filePath);
        string extension = Path.GetExtension(filePath);

        int count = 1;
        string candidate = filePath;

        while (File.Exists(candidate))
        {
            candidate = Path.Combine(directory, $"{fileName} ({count}){extension}");
            count++;
        }

        return candidate;
    }

    public string EnsureFfmpegDirectoryExists(string? expectedPath)
    {
        string targetPath = expectedPath ?? "";

        if (string.IsNullOrWhiteSpace(targetPath) || targetPath.Contains("Unknown", StringComparison.OrdinalIgnoreCase))
        {
            var baseDir = AppContext.BaseDirectory;
            string subDir = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "win-x64" :
                            RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "osx-x64" : "linux-x64";
            targetPath = Path.Combine(baseDir, "ffmpeg", subDir);
        }

        if (!Directory.Exists(targetPath))
        {
            Directory.CreateDirectory(targetPath);
        }

        return targetPath;
    }

}
using ExCSS;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Video_Size_Optimizer.Services;



public class FileService
{
    public (long totalSize, List<string> videoPaths) GetFolderData(string folder)
    {
        var extensions = new[] {
                                    ".mp4", ".mkv", ".mov", ".avi", ".webm", ".m4v",
                                    ".flv", ".wmv",
                                    ".mpg", ".mpeg",
                                    ".ts", ".mts", ".m2ts",
                                    ".3gp", ".3g2",
                                    ".ogv", ".vob", ".asf", ".f4v"
                                };
        long totalSize = 0;
        var videoPaths = new List<string>();

        var allFiles = Directory.EnumerateFiles(folder, "*.*", SearchOption.AllDirectories);

        foreach (var file in allFiles)
        {
            var info = new FileInfo(file);
            totalSize += info.Length;

            if (extensions.Contains(info.Extension.ToLower()))
            {
                videoPaths.Add(file);
            }
        }

        return (totalSize, videoPaths);
    } 

    public string GenerateOutputPath(string inputPath, int crfValue)
    {
        return BuildFinalPath(inputPath, $"-CRF{crfValue}");
    }

    public string GenerateTargetSizePath(string inputPath, int targetMb)
    {
        return BuildFinalPath(inputPath, $"-Target{targetMb}MB");
    }

    private string BuildFinalPath(string inputPath, string suffix)
    {
        string directory = Path.GetDirectoryName(inputPath) ?? "";
        string fileNameOnly = Path.GetFileNameWithoutExtension(inputPath);
        // .mp4 Alt: Path.GetExtension(inputPath) to keep original format
        string extension = ".mp4";

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
}
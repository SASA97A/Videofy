using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Video_Size_Optimizer.Services;



public class FileService
{
    public (long totalSize, List<string> videoPaths) GetFolderData(string folder)
    {
        var extensions = new[] { ".mp4", ".mkv", ".mov", ".avi", ".webm", ".m4v" };
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
        string directory = Path.GetDirectoryName(inputPath) ?? "";
        string fileNameOnly = Path.GetFileNameWithoutExtension(inputPath);
        string candidatePath = Path.Combine(directory, $"{fileNameOnly}-CRF{crfValue}.mp4");

        return GetUniqueFilePath(candidatePath);
    }

    public string GetUniqueFilePath(string filePath)
    {
        if (!File.Exists(filePath)) return filePath;

        string directory = Path.GetDirectoryName(filePath) ?? "";
        string fileName = Path.GetFileNameWithoutExtension(filePath);
        string extension = Path.GetExtension(filePath);

        int count = 1;
        string newPath = filePath;

        while (File.Exists(newPath))
        {
            newPath = Path.Combine(directory, $"{fileName} ({count}){extension}");
            count++;
        }

        return newPath;
    }
}
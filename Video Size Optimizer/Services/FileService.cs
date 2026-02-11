using System.Collections.Generic;
using System.IO;


namespace Video_Size_Optimizer.Services;



public class FileService
{
    public (long totalSize, List<string> videoPaths) GetFolderData(string folder, HashSet<string> allowedExtensions)
    {
        long totalSize = 0;
        var videoPaths = new List<string>();

        var allFiles = Directory.EnumerateFiles(folder, "*.*", SearchOption.AllDirectories);

        foreach (var file in allFiles)
        {
            var info = new FileInfo(file);
            totalSize += info.Length;

            if (allowedExtensions.Contains(info.Extension))
            {
                videoPaths.Add(file);
            }
        }

        return (totalSize, videoPaths);
    }

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

    public string GenerateSplitPatternPath(string inputPath, string extension)
    {
        string directory = Path.GetDirectoryName(inputPath) ?? "";
        string fileName = Path.GetFileNameWithoutExtension(inputPath);

        if (!string.IsNullOrEmpty(extension) && !extension.StartsWith("."))
        {
            extension = "." + extension;
        }

        // Returns: C:\Path\Video_part%03d.mp4
        return Path.Combine(directory, $"{fileName}_part%03d{extension}");
    }


}
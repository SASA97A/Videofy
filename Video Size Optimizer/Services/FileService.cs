using System;
using System.Collections.Generic;
using System.IO;
using Video_Size_Optimizer.Utils;


namespace Video_Size_Optimizer.Services;



public class FileService
{
    public (long totalSize, List<string> videoPaths) GetFolderData(string folder, HashSet<string> allowedExtensions)
    {
        long totalSize = 0;
        var videoPaths = new List<string>();

        // Skip subfolders the user cannot open (for example "C:\Documents and Settings")
        // instead of throwing UnauthorizedAccessException and aborting the whole scan.
        // AttributesToSkip = 0 keeps hidden and system files, as SearchOption.AllDirectories did.
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = 0
        };

        foreach (var file in Directory.EnumerateFiles(folder, "*", options))
        {
            try
            {
                var info = new FileInfo(file);
                totalSize += info.Length;

                if (allowedExtensions.Contains(info.Extension))
                {
                    videoPaths.Add(file);
                }
            }
            catch (IOException)
            {
                // The file was deleted or locked during the scan. Leave it out.
            }
            catch (UnauthorizedAccessException)
            {
                // No permission to read this file's details. Leave it out.
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
        return BuildFinalPath(inputPath, $"-CRF{crfValue}", ResolveOutputExtension(inputPath, extension));
    }

    public string GenerateTargetSizePath(string inputPath, int targetMb, string extension)
    {
        return BuildFinalPath(inputPath, $"-Target{targetMb}MB", ResolveOutputExtension(inputPath, extension));
    }

    private string ResolveOutputExtension(string inputPath, string requestedFormat)
    {
        if (string.IsNullOrWhiteSpace(requestedFormat) || requestedFormat == AppConstants.OriginalFormat)
        {
            string sourceExtension = Path.GetExtension(inputPath);
            if (!string.IsNullOrWhiteSpace(sourceExtension)) return sourceExtension;
        }

        return string.IsNullOrWhiteSpace(requestedFormat) ? ".mp4" : requestedFormat;
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
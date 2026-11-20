using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using System.Threading.Tasks;
using Video_Size_Optimizer.Utils;


namespace Video_Size_Optimizer.Services;



public class FileService
{
    /// <summary>
    /// Lists the videos under <paramref name="folder"/> and the total size of all files in it.
    /// <paramref name="progress"/>, if given, receives the number of files checked and videos found so far.
    /// </summary>
    public (long totalSize, List<string> videoPaths) GetFolderData(string folder, HashSet<string> allowedExtensions,
        IProgress<(int FilesChecked, int VideosFound)>? progress = null)
    {
        long totalSize = 0;
        int filesChecked = 0;
        var videoPaths = new List<string>();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // IgnoreInaccessible: skip folders the user cannot open (for example "C:\Documents and Settings")
        // instead of throwing UnauthorizedAccessException and aborting the whole scan.
        // AttributesToSkip = 0 keeps hidden and system files, as SearchOption.AllDirectories did.
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = 0
        };

        // FileSystemEnumerable reads each file's size from the directory listing itself.
        // Creating a FileInfo per file costs one extra disk call each and made a full C:\ scan
        // about 15 times slower (minutes instead of about 30 seconds).
        var entries = new FileSystemEnumerable<(string Path, long Length)>(folder,
            (ref FileSystemEntry entry) => (entry.ToFullPath(), entry.Length), options)
        {
            ShouldIncludePredicate = (ref FileSystemEntry entry) => !entry.IsDirectory,
            // Do not follow junctions or symbolic links to folders. They can point back up the tree
            // (endless loops) or list the same files twice (for example node_modules links).
            ShouldRecursePredicate = (ref FileSystemEntry entry) => (entry.Attributes & FileAttributes.ReparsePoint) == 0
        };

        // ".ts"/".mts" files that need their first bytes read to tell a video from TypeScript code.
        // They are read after the listing, in parallel, because opening files one by one is slow.
        var transportStreamCandidates = new List<string>();

        foreach (var (path, length) in entries)
        {
            totalSize += length;
            filesChecked++;

            string extension = Path.GetExtension(path);
            if (allowedExtensions.Contains(extension))
            {
                if (!IsAmbiguousTransportStreamExtension(extension))
                    videoPaths.Add(path);
                else if (CouldBeTransportStream(path, length))
                    transportStreamCandidates.Add(path);
            }

            if (progress != null && filesChecked % 5000 == 0)
            {
                progress.Report((filesChecked, videoPaths.Count));
            }
        }

        if (transportStreamCandidates.Count > 0)
        {
            var confirmed = new System.Collections.Concurrent.ConcurrentBag<string>();
            Parallel.ForEach(transportStreamCandidates, new ParallelOptions { MaxDegreeOfParallelism = 8 }, path =>
            {
                if (HasTransportStreamHeader(path)) confirmed.Add(path);
            });
            videoPaths.AddRange(confirmed.OrderBy(p => p, StringComparer.OrdinalIgnoreCase));
        }

        LogService.Instance.Log(
            $"Scanned {filesChecked:N0} file(s) in {stopwatch.Elapsed.TotalSeconds:F1}s | Path={folder}" +
            $" | .ts/.mts read={transportStreamCandidates.Count:N0}", LogLevel.Info, "Scan");

        return (totalSize, videoPaths);
    }

    // ".ts" and ".mts" are video extensions (MPEG transport stream) but also TypeScript source
    // extensions. A scan of a drive with code projects listed about 150,000 TypeScript files as videos.
    private static bool IsAmbiguousTransportStreamExtension(string extension) =>
        extension.Equals(".ts", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".mts", StringComparison.OrdinalIgnoreCase);

    // Two transport stream packets: 188 bytes (.ts) or 192 bytes (.m2ts/.mts camcorder files).
    private const int TransportStreamProbeBytes = 2 * 192;

    /// <summary>Cheap checks that reject most TypeScript files without opening them.</summary>
    private static bool CouldBeTransportStream(string path, long length)
    {
        if (length < TransportStreamProbeBytes) return false;
        if (path.EndsWith(".d.ts", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".d.mts", StringComparison.OrdinalIgnoreCase)) return false;

        // Package folders of JavaScript projects hold code, not recordings.
        string sep = Path.DirectorySeparatorChar.ToString();
        return !path.Contains($"{sep}node_modules{sep}", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Reads the first two packets and checks for the 0x47 sync byte that starts each one.</summary>
    private static bool HasTransportStreamHeader(string path)
    {
        try
        {
            Span<byte> head = stackalloc byte[TransportStreamProbeBytes];
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 1);
            if (stream.ReadAtLeast(head, TransportStreamProbeBytes, throwOnEndOfStream: false) < TransportStreamProbeBytes)
                return false;

            const byte Sync = 0x47;
            bool plainTs = head[0] == Sync && head[188] == Sync;
            bool m2ts = head[4] == Sync && head[196] == Sync;
            return plainTs || m2ts;
        }
        catch (Exception)
        {
            // Unreadable now: keep it, so a locked real video is not silently hidden.
            return true;
        }
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
using CommunityToolkit.Mvvm.ComponentModel;
using System.IO;

namespace Video_Size_Optimizer.Models;

public partial class VideoFile : ObservableObject
{
    public string FilePath { get; }
    public string FileName => Path.GetFileName(FilePath);
    public bool ShowIndeterminate => IsProcessing && Progress <= 0;
    public bool IsReady => !IsProcessing && !IsCompleted;
    public long RawSizeBytes { get; private set; }
    partial void OnProgressChanged(double value) => OnPropertyChanged(nameof(ShowIndeterminate));
    partial void OnIsProcessingChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowIndeterminate));
        OnPropertyChanged(nameof(IsReady));
    }
    partial void OnIsCompletedChanged(bool value) => OnPropertyChanged(nameof(IsReady));

    [ObservableProperty] private string folderName = string.Empty;
    [ObservableProperty] private string fileSizeDisplay = "";
    [ObservableProperty] private bool isSelected;
    [ObservableProperty] private double progress = 0;
    [ObservableProperty] private bool isProcessing;
    [ObservableProperty] private bool isCompleted;
    

    public VideoFile(string filePath, string rootFolder)
    {
        FilePath = filePath;
        var info = new FileInfo(filePath);
        RawSizeBytes = info.Length;
        FileSizeDisplay = $"{(info.Length / 1024.0 / 1024.0):F2} MB";

        string relativePath = Path.GetRelativePath(rootFolder, Path.GetDirectoryName(filePath) ?? "");
        FolderName = relativePath == "." ? "Root" : relativePath;
    }

    private void FormatFileSize(string path)
    {
        var info = new FileInfo(path);
        double sizeInMb = info.Length / 1024.0 / 1024.0;
        FileSizeDisplay = $"{sizeInMb:F2} MB";
    }
}


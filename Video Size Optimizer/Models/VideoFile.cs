using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.IO;
using Video_Size_Optimizer.ViewModels;

namespace Video_Size_Optimizer.Models;

public partial class VideoFile : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FileName))] 
    private string _filePath;
    public string FileName => Path.GetFileName(FilePath);

    //Standard Process
    
    public long RawSizeBytes { get; private set; }
    public double MaxTargetMb => Math.Ceiling(RawSizeBytes / (1024.0 * 1024.0) * 0.90);
    public bool IsAlreadySmall => MaxTargetMb <= 10; 

    [ObservableProperty] private string folderName = string.Empty;
    [ObservableProperty] private string fileSizeDisplay = "";
    [ObservableProperty] private bool isSelected;

    public void UpdateStatusSize(double newSizeMb)
    {
        // This triggers the NotifyPropertyChanged event properly
        FileSizeDisplay = $"{(RawSizeBytes / 1024.0 / 1024.0):F2} MB -> {newSizeMb:F2} MB";
    }

    // Per-File Custom settings
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCustomSize))]
    [NotifyPropertyChangedFor(nameof(HasCustomSettings))]
    [NotifyPropertyChangedFor(nameof(CustomSettingsBadge))]
    [NotifyPropertyChangedFor(nameof(CustomTargetSizeMbSlider))]
    private int? _customTargetSizeMb;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCustomSettings))]
    [NotifyPropertyChangedFor(nameof(CustomSettingsBadge))]
    private string? _customResolution;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCustomSettings))]
    [NotifyPropertyChangedFor(nameof(CustomSettingsBadge))]
    private string? _customFps;

    public double CustomTargetSizeMbSlider
    {
        get => CustomTargetSizeMb ?? 5;
        set => CustomTargetSizeMb = value <= 5 ? null : (int)value;
    }
    public bool HasCustomSize => CustomTargetSizeMb.HasValue;

    [ObservableProperty]
   // [NotifyPropertyChangedFor(nameof(HasCustomSettings))]
   // [NotifyPropertyChangedFor(nameof(CustomSettingsBadge))]
    private int _splitSizeMb = 0;

    public bool IsSplitEnabled => SplitSizeMb >= 5;

    public bool HasCustomSettings => HasCustomSize || !string.IsNullOrEmpty(CustomResolution)
                                     || !string.IsNullOrEmpty(CustomFps) || IsTrimmed ;



    public VideoFile(string filePath, string rootFolder)
    {
        _filePath = filePath;
        var info = new FileInfo(filePath);
        RawSizeBytes = info.Length;
        FileSizeDisplay = $"{(info.Length / 1024.0 / 1024.0):F2} MB";

        string rootFolderName = Path.GetFileName(rootFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        string relativePath = Path.GetRelativePath(rootFolder, Path.GetDirectoryName(filePath) ?? "");
        FolderName = relativePath == "." ? rootFolderName + (" (Root Folder)") : Path.Combine(rootFolderName, relativePath);
    }
    

}


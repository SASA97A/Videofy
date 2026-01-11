using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Video_Size_Optimizer.Models;
using Video_Size_Optimizer.Services;

namespace Video_Size_Optimizer.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    //Instances
    private readonly FfmpegService _ffmpegService = new();
    private readonly FfprobeService _ffprobeService = new();
    private readonly FileService _fileService = new();
    private readonly SystemUtilityService _systemService = new();
    private readonly MessageService _messageService = new();


    public bool HasVideos => Videos.Count > 0;
    public string SelectionStatus => $"{Videos.Count(v => v.IsSelected)} of {Videos.Count} files selected";
    public string ActionButtonText => IsBusy ? "STOP COMPRESSION" : "START COMPRESSION";
    public string ActionButtonColor => IsBusy ? "#ff4444" : "#00d26a";
    public ObservableCollection<VideoFile> Videos { get; } = new();
    public ObservableCollection<VideoFile> DisplayedVideos { get; } = new();

    public string CrfDescription => AppConstants.GetCrfLabel(CrfValue);
    public List<string> FpsOptions => AppConstants.FpsOptions;
    partial void OnCrfValueChanged(int value) => OnPropertyChanged(nameof(CrfDescription));

    [ObservableProperty] private int crfValue = 28;
    [ObservableProperty] private string statusMessage = "Ready";
    [ObservableProperty] private string _selectedFolderPath = "None";
    [ObservableProperty] private string _totalFolderSizeDisplay = "0 GB";
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _selectedEncoder = "Standard (Slow, Best Quality)";
    [ObservableProperty] private string _selectedResolution = "Original Resolution";
    [ObservableProperty] private string _selectedFps = "Original";
    [ObservableProperty] private bool _showDependencyWarning;
    [ObservableProperty] private string _expectedPath = "";
    [ObservableProperty] private bool _stripMetadata = true;
    [ObservableProperty] private bool _isCrfMode = true;
    [ObservableProperty] private int _targetSizeMb = 25;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(PauseActionIconPath))]
    private bool _isPaused;
    [ObservableProperty] 
    [NotifyPropertyChangedFor(nameof(ActionButtonText))]
    [NotifyPropertyChangedFor(nameof(ActionButtonColor))]
    private bool _isBusy;

    public string PauseActionIconPath => IsPaused
    ? "avares://Videofy/Assets/play.svg"
    : "avares://Videofy/Assets/pause.svg";

    public List<string> AvailableEncoders => AppConstants.AvailableEncoderNames;
    public List<string> ResolutionOptions => AppConstants.AvailableResolutionNames;

    public IRelayCommand<Window> BrowseFolderCommand { get; }
    public IRelayCommand SelectAllCommand { get; }
    public IRelayCommand DeselectAllCommand { get; }
    public IAsyncRelayCommand StartCommand { get; }

    public MainWindowViewModel()
    {

        BrowseFolderCommand = new RelayCommand<Window>(BrowseFolder);

        SelectAllCommand = new RelayCommand(() =>
        {
            foreach (var v in Videos)
                v.IsSelected = true;
        });

        DeselectAllCommand = new RelayCommand(() =>
        {
            foreach (var v in Videos)
                v.IsSelected = false;
        });

        StartCommand = new AsyncRelayCommand(StartCompressionAsync);

        if (!DependencyChecker.CheckBinaries(out string path))
        {
            ShowDependencyWarning = true;
            ExpectedPath = path;
            StatusMessage = "Setup Required: Missing FFmpeg files.";
        }
    }

    // Check for updates
    [RelayCommand]
    public async Task CheckForUpdates()
    {    
        var latestVersion = await _systemService.GetLatestGithubTagNameAsync("SASA97A", "Videofy");

        if (latestVersion == null)
        {
            await _messageService.ShowErrorAsync("Check Failed", "Could not connect to GitHub!");
            return;
        }
        if (latestVersion != AppConstants.AppVersion)
        {
            bool update = await _messageService.ShowYesNoAsync("Update Available",
            $"Version {latestVersion} is available! \n\nWould you like to open the download page?");
            if (update) _systemService.OpenAppWebLink(AppLink.GitHub);
        }
        else
        {
            await _messageService.ShowSuccessAsync("Up to Date", $"You are on the latest version ({AppConstants.AppVersion}).");
        }
    }


    private async void BrowseFolder(Window? parentWindow)
    {
        if (parentWindow == null)
        {
            await _messageService.ShowErrorAsync("System Error",
                "The application could not detect the main window. Please restart the app and try again.");
            return;
        }

        // 1. Use the new StorageProvider API
        var options = new FolderPickerOpenOptions
        {
            Title = "Select folder containing videos",
            AllowMultiple = false
        };

        var result = await parentWindow.StorageProvider.OpenFolderPickerAsync(options);

        // 2. The result is a list (even for single selection), check if user picked anything
        if (result == null || result.Count == 0) return;

        // 3. Convert the StorageFolder to a local path string
        var folderPath = result[0].TryGetLocalPath();

        if (string.IsNullOrEmpty(folderPath))
        {
            await _messageService.ShowErrorAsync("Folder Access Error", AppConstants.NoFolderAccess);
            return;
        }

        Videos.Clear();
        SelectedFolderPath = folderPath;

        var data = _fileService.GetFolderData(folderPath);
        TotalFolderSizeDisplay = $"{(data.totalSize / 1024.0 / 1024.0 / 1024.0):F2} GB";

        foreach (var path in data.videoPaths)
        {
            var video = new VideoFile(path, folderPath);
            video.PropertyChanged += VideoFile_PropertyChanged;
            Videos.Add(video);
        }

        ApplyFilter();
        RefreshStats();
    }

    private void VideoFile_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(VideoFile.IsSelected))
        {
            RefreshStats();
        }
    }

    private void RefreshStats()
    {
        OnPropertyChanged(nameof(HasVideos));
        OnPropertyChanged(nameof(SelectionStatus));
    }

    private async Task StartCompressionAsync()
    {

        var selectedVideos = Videos.Where(v => v.IsSelected &&
                                         !v.IsCompleted &&
                                         !v.FileName.Contains("-CRF", StringComparison.OrdinalIgnoreCase)).ToList();
        if (selectedVideos.Count == 0)
        {
            await _messageService.ShowInfoAsync("No Selection", AppConstants.NoSelectionMessage);
            return;
        }

        // Platform Permission Checks (Linux/macOS)
        var ffmpegError = _ffmpegService.InitializePermissions();
        if (ffmpegError != null)
        {
            await _messageService.ShowErrorAsync("Platform Permission Error", ffmpegError);
            return;
        }

        var ffprobeError = _ffprobeService.InitializePermissions();
        if (ffprobeError != null)
        {
            await _messageService.ShowErrorAsync("Platform Permission Error", ffprobeError);
            return;
        }

        int completedCount = 0;
        long totalBytesSaved = 0;


        IsBusy = true;
        StatusMessage = "Processing...";

        try
        {
            foreach (var video in selectedVideos)
            {
                if (!IsBusy) break;

                video.IsCompleted = false;
                video.Progress = 0;
                video.IsProcessing = true;

                await Task.Delay(50);
                

                try
                {
                    long originalSizeBytes = new FileInfo(video.FilePath).Length;

                    // Get original width
                    int originalWidth = await _ffprobeService.GetVideoWidthAsync(video.FilePath);

                    string resolutionToUse = "Original";
                    if (AppConstants.ResolutionMap.TryGetValue(SelectedResolution, out string? targetValue))
                    {
                        if (targetValue == "Original")
                        {
                            resolutionToUse = "Original";
                        }
                        else if (int.TryParse(targetValue, out int targetWidth))
                        {
                            // Only downscale; if source is smaller than target, keep Original
                            resolutionToUse = targetWidth < originalWidth ? targetValue : "Original";
                        }
                    }

                    // Determine Encoder
                    if (!AppConstants.EncoderMap.TryGetValue(SelectedEncoder, out string? encoderValue))
                    {
                        encoderValue = "libx265";
                    }
                    // Ensure we don't overwrite
                    string finalOutputPath = _fileService.GenerateOutputPath(video.FilePath, CrfValue);

                    //Run Compression
                    var p = new Progress<double>(val => video.Progress = val);
                    await _ffmpegService.CompressAsync(video.FilePath, finalOutputPath, SelectedFps, StripMetadata, CrfValue, encoderValue, resolutionToUse, p);

                    // Update UI with the actual new file size
                    if (File.Exists(finalOutputPath))
                    {
                        var newInfo = new FileInfo(finalOutputPath);
                        totalBytesSaved += (originalSizeBytes - newInfo.Length);
                        completedCount++;

                        double newSize = newInfo.Length / 1024.0 / 1024.0;
                        // Using actual filename in case it was renamed to "File (1).mp4"
                        video.FileSizeDisplay += $" -> {newSize:F2} MB ({Path.GetFileName(finalOutputPath)})";
                    }

                    video.Progress = 100;
                    video.IsCompleted = true;
                }
                catch (Exception)
                {
                    if (IsBusy) StatusMessage = $"Error: {video.FileName} failed.";
                }
                finally
                {
                    video.IsProcessing = false;
                }
            }
            //Show completion Message
            if (completedCount > 0)
               {
                   double savedMb = totalBytesSaved / 1024.0 / 1024.0;
                   string sizeDisplay = savedMb > 1024 ? $"{(savedMb / 1024.0):F2} GB" : $"{savedMb:F2} MB";

                   await _messageService.ShowSuccessAsync("Task Completed",
                       $"Successfully processed {completedCount} videos.\n\nTotal space saved: {sizeDisplay}");
               }
        }
        finally
        {
            IsBusy = false;
            StatusMessage = "Ready";
        }
    }
    // Show videos on the grid
    private void ApplyFilter()
    {
        var filtered = Videos.Where(v =>
            string.IsNullOrWhiteSpace(SearchText) ||
            v.FileName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
            v.FolderName.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Only update if something actually changed to prevent flickering
        DisplayedVideos.Clear();
        foreach (var item in filtered)
        {
            DisplayedVideos.Add(item);
        }
    }

    // Update the Filtered list whenever SearchText or the underlying Videos change
    partial void OnSearchTextChanged(string value) => ApplyFilter();

    [RelayCommand]
    private void ToggleSelectAll()
    {
        var visibleItems = DisplayedVideos.ToList();

        if (!visibleItems.Any()) return;

        bool targetState = !visibleItems.All(v => v.IsSelected);

        foreach (var video in visibleItems)
        {
            video.IsSelected = targetState;
        }

        RefreshStats();
    }

    

    [RelayCommand]
    public async Task StopAllProcessing(bool skipConfirmation = false)
    {

        // Only show the message if we are NOT skipping confirmation
        bool confirm = true;

        if (!skipConfirmation)
        {
            confirm = await _messageService.ShowYesNoAsync(
                "Stop Processing?",
                "Are you sure you want to stop the compression process? Any file currently being processed will be incomplete.");
        }

        //  When confirmed 
        if (confirm)
        {
            _ffprobeService.KillProcess();
            _ffmpegService.KillProcess();
            IsBusy = false;
            StatusMessage = "Process terminated by user.";
        }
    }

    public bool RequestClose()
    {
        return !IsBusy;
    }

    [RelayCommand]
    public async Task ShowAbout()
    {
        await _messageService.ShowInfoAsync("About Videofy", AppConstants.AboutMessage);
    }

    [RelayCommand]
    public void ClearVideos()
    {
        Videos.Clear();
        OnPropertyChanged(nameof(HasVideos));
    }

    [RelayCommand]
    public void ClearList()
    {
        foreach (var v in Videos)
            v.PropertyChanged -= VideoFile_PropertyChanged;

        Videos.Clear();
        ApplyFilter();
        RefreshStats();
    }

    [RelayCommand]
    public void RemoveVideo(VideoFile video)
    {
        if (video != null)
        {
            video.PropertyChanged -= VideoFile_PropertyChanged;
            Videos.Remove(video);
            RefreshStats();
        }
    }

    [RelayCommand]
    private void TogglePause()
    {
        IsPaused = !IsPaused;
        _ffmpegService.TogglePause(IsPaused);

        if (StatusMessage == "Processing..." || StatusMessage == "Paused" || StatusMessage == "Ready")
        {
            StatusMessage = IsPaused ? "Paused" : "Processing...";
        }
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task HandleActionAsync()
    {
        if (IsBusy)
        {
           await StopAllProcessing();
        }
        else
        {
            await StartCompressionAsync();
        }
    }

    [RelayCommand]
    public void RefreshFolder()
    {
        if (string.IsNullOrEmpty(SelectedFolderPath) || SelectedFolderPath == "None")
            return;

        foreach (var v in Videos)
            v.PropertyChanged -= VideoFile_PropertyChanged;

        Videos.Clear();

        var data = _fileService.GetFolderData(SelectedFolderPath);
        TotalFolderSizeDisplay = $"{(data.totalSize / 1024.0 / 1024.0 / 1024.0):F2} GB";

        foreach (var path in data.videoPaths)
        {
            var video = new VideoFile(path, SelectedFolderPath);
            video.PropertyChanged += VideoFile_PropertyChanged;
            Videos.Add(video);
        }

        ApplyFilter();
        RefreshStats();
    }

    [RelayCommand]
    public void OpenBtbN()
    {
        _systemService.OpenAppWebLink(AppLink.BtbNReleases);
    }

    [RelayCommand]
    public void OpenExpectedFolder()
    {
        _systemService.OpenLocalFolder(ExpectedPath);
    }

    [RelayCommand]
    public void RefreshDependencyCheck()
    {
        // Re-run the binary check
        if (DependencyChecker.CheckBinaries(out string path))
        {
            ShowDependencyWarning = false;
            StatusMessage = "Binaries found! Ready to go.";
        }
        else
        {
            ExpectedPath = path;
            StatusMessage = "Files still missing. Please check the folder again.";
        }
    }

}

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
using Video_Size_Optimizer.Utils;
using Video_Size_Optimizer.Services;
using System.Globalization;

namespace Video_Size_Optimizer.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    //Instances
    private readonly FfmpegService _ffmpegService = new();
    private readonly FfprobeService _ffprobeService = new();
    private readonly FileService _fileService = new();
    private readonly SystemUtilityService _systemService = new();
    private readonly MessageService _messageService = new();
    private readonly SettingsService _settingsService = new();
    [ObservableProperty] private AppSettings _globalSettings = new();
    [ObservableProperty] private string _conversionTargetFormat = ".mp4";

    public bool HasVideos => Videos.Count > 0;
    public string SelectionStatus => $"{Videos.Count(v => v.IsSelected)} of {Videos.Count} files selected";
    public string ActionButtonText => IsBusy ? "STOP PROCESSING" : "START PROCESSING";
    public string ActionButtonColor => IsBusy ? "#ff4444" : "#00d26a";
    public ObservableCollection<VideoFile> Videos { get; } = new();
    public ObservableCollection<VideoFile> DisplayedVideos { get; } = new();

    public string CrfDescription => AppConstants.GetCrfLabel(CrfValue);
    public List<string> FpsOptions => AppConstants.FpsOptions;
    public List<string> AvailableEncoders => GlobalSettings.EnabledEncoders;
    public List<string> ResolutionOptions => AppConstants.AvailableResolutionNames;
    public List<string> OutputFormats => AppConstants.AvailableFormats;
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
    [ObservableProperty] private bool _isDownloading;
    [ObservableProperty] private string _currentSpeed = "0x";
    [ObservableProperty] private int _selectedTabIndex;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(PauseActionIconPath))]
    private bool _isPaused;
    [ObservableProperty] 
    [NotifyPropertyChangedFor(nameof(ActionButtonText))]
    [NotifyPropertyChangedFor(nameof(ActionButtonColor))]
    private bool _isBusy;

    public string PauseActionIconPath => IsPaused
    ? "avares://Videofy/Assets/play.svg"
    : "avares://Videofy/Assets/pause.svg";

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

        GlobalSettings = _settingsService.LoadSettings();
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

        var allowedExtensions = AppConstants.GetCombinedExtensions(GlobalSettings.CustomExtensions);
        var data = _fileService.GetFolderData(folderPath, allowedExtensions);
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
        var selectedVideos = Videos.Where(v => {
            // Basic requirements
            if (!v.IsSelected || v.IsCompleted) return false;

            if (!GlobalSettings.ProcessAlreadyOptimized)
            {
                bool isAlreadyOptimized = v.FileName.Contains("-CRF", StringComparison.OrdinalIgnoreCase) ||
                                         v.FileName.Contains("-Target", StringComparison.OrdinalIgnoreCase);
                if (isAlreadyOptimized) return false;
            }
            return true;
        }).ToList();

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
        if (GlobalSettings.PreventSleep)
            await _systemService.PreventSleepAsync(true, _messageService.ShowErrorAsync);

        try
        {
            foreach (var video in selectedVideos)
            {
                if (!IsBusy) break;

                if (IsDiskSpaceLow())
                {
                    _ffmpegService.TogglePause(true);
                    if (!IsPaused) await TogglePause();
                    StatusMessage = "Paused: Low Disk Space!";
                    await _messageService.ShowErrorAsync("Low Disk Space",
                        $"Available space is below {GlobalSettings.LowDiskBufferGb}GB. " +
                        $"The process has been paused. Please free up space and click Resume.");
                    while (IsPaused)
                    {
                        if (!IsBusy) break; // If user "Stop" instead of "Resume"
                        await Task.Delay(1000); 
                    }

                    if (!IsBusy)
                    {
                        IsPaused = false;
                        break;
                    }
                }

                video.IsCompleted = false;
                video.Progress = 0;
                video.IsProcessing = true;

                await Task.Delay(50);

                try
                {
                    if (video.DurationSeconds <= 0)
                    {
                        video.DurationSeconds = await _ffprobeService.GetVideoDurationAsync(video.FilePath);
                    }

                    long originalSizeBytes = new FileInfo(video.FilePath).Length;

                    // Get original width
                    int originalWidth = await _ffprobeService.GetVideoWidthAsync(video.FilePath);

                    string chosenResname = !string.IsNullOrEmpty(video.CustomResolution)
                                            ? video.CustomResolution : SelectedResolution;

                    string resolutionToUse = "Original";
                    if (chosenResname != "Original Resolution" &&
                        AppConstants.ResolutionMap.TryGetValue(chosenResname, out string? targetValue))
                    {
                        if (int.TryParse(targetValue, out int targetWidth))
                        {
                            resolutionToUse = targetWidth < originalWidth ? targetWidth.ToString() : "Original";
                        }
                    }

                    string fpsToUse = !string.IsNullOrEmpty(video.CustomFps)
                                       ? video.CustomFps : SelectedFps;

                    // Determine Encoder
                    if (!AppConstants.EncoderMap.TryGetValue(SelectedEncoder, out string? encoderValue))
                    {
                        encoderValue = "libx265";
                    }
                    //Ensure we don't override files
                    string finalOutputPath;

                    //Run Compression
                    var p = new Progress<ConversionProgress>(cp =>
                    {
                        video.UpdateProgress(cp.Percentage, cp.Speed, cp.Fps);
                        CurrentSpeed = cp.Speed;
                    });

                    string trimArgs = "";
                    if (video.IsTrimmed)
                    {
                        trimArgs = $"-ss {video.StartTime.ToString(CultureInfo.InvariantCulture)} ";
                        if (video.EndTime != 0.0000)
                        {
                            trimArgs += $"-to {video.EndTime.ToString(CultureInfo.InvariantCulture)} ";
                        }
                    }

                    if (SelectedTabIndex == 1)
                    {
                        finalOutputPath = _fileService.GenerateOutputPath(video.FilePath, 0, ConversionTargetFormat);
                        await _ffmpegService.CompressAsync(video.FilePath, finalOutputPath,
                            "Original", false, 0, "copy", "Original", trimArgs, p);
                    }
                    else
                    {
                        finalOutputPath = video.HasCustomSize
                                            ? _fileService.GenerateTargetSizePath(video.FilePath, (int)video.CustomTargetSizeMb!, GlobalSettings.DefaultOutputFormat)
                                            : _fileService.GenerateOutputPath(video.FilePath, CrfValue, GlobalSettings.DefaultOutputFormat);

                        if (video.HasCustomSize)
                        {
                            if (video.DurationSeconds > 0)
                            {
                                // 2. Run target size compression 
                                await _ffmpegService.CompressTargetSizeAsync(video.FilePath,
                                    finalOutputPath, fpsToUse, StripMetadata,
                                    (int)video.CustomTargetSizeMb!, encoderValue,
                                    resolutionToUse, video.DurationSeconds, trimArgs, p);
                            }
                        }
                        else
                        {
                            // Standard Batch Process - Uses Global CRF
                            await _ffmpegService.CompressAsync(video.FilePath, finalOutputPath,
                                fpsToUse, StripMetadata, CrfValue, encoderValue, resolutionToUse, trimArgs, p);
                        }
                    }
                 
                    // Important to avoid deleteing files if user Stops compression!
                    if (!IsBusy)
                    {                      
                        if (File.Exists(finalOutputPath)) File.Delete(finalOutputPath);
                        break;
                    }

                    if (File.Exists(finalOutputPath))
                    {
                        var newInfo = new FileInfo(finalOutputPath);

                        if (newInfo.Length != 0)
                        {
                            totalBytesSaved += (originalSizeBytes - newInfo.Length);
                            completedCount++;

                            double newSize = newInfo.Length / 1024.0 / 1024.0;
                            video.UpdateStatusSize(newSize);

                            video.Progress = 100;
                            video.IsCompleted = true;

                            var index = DisplayedVideos.IndexOf(video);
                            if (index != -1)
                            {
                                // This "re-seats" the item in the collection, forcing the row to refresh
                                DisplayedVideos[index] = video;
                            }

                            if (SelectedTabIndex != 1 && GlobalSettings.DeleteOriginalAfterCompression && File.Exists(finalOutputPath))
                            {
                                try
                                {
                                    File.Delete(video.FilePath);
                                }
                                catch (Exception)
                                {
                                    // Log later
                                }
                            }
                        }                                                   
                    }                                     
                }
                catch (Exception)
                {
                    if (IsBusy) StatusMessage = $"Error: {video.FileName} failed.";
                }
                finally
                {
                    video.IsProcessing = false;
                    if (!IsBusy) IsPaused = false;
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
            await _systemService.PreventSleepAsync(false);
            IsBusy = false;
            StatusMessage = "Ready";
            CurrentSpeed = "0x";
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

    private bool IsDiskSpaceLow()
    {
        try
        {
            // Get the drive 
            string driveName = Path.GetPathRoot(SelectedFolderPath) ?? "";
            DriveInfo drive = new DriveInfo(driveName);

            long bufferBytes = (long)GlobalSettings.LowDiskBufferGb * 1024 * 1024 * 1024;
            return drive.AvailableFreeSpace < bufferBytes;
        }
        catch { return false; } // Safety fallback
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
            await _systemService.PreventSleepAsync(false);
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
    private async Task TogglePause()
    {

        if (IsPaused)
        {
            if (IsDiskSpaceLow())
            {
                await _messageService.ShowErrorAsync("Resume Blocked",
                    $"Still low on disk space (under {GlobalSettings.LowDiskBufferGb}GB). Please free up space before resuming.");
                return;
            }
        }

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

        var allowedExts = AppConstants.GetCombinedExtensions(GlobalSettings.CustomExtensions);
        var data = _fileService.GetFolderData(SelectedFolderPath, allowedExts);
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
    public async Task OpenExpectedFolder()
    {
        try
        {
            AppPathService.EnsureDirectories();
            _systemService.OpenLocalFolder(AppPathService.FfmpegBinFolder);
        }
        catch (Exception ex)
        {
           await  _messageService.ShowErrorAsync("Folder Error", $"Could not prepare or open the folder: {ex.Message}");
        }
    }

    [RelayCommand]
    public void RefreshDependencyCheck()
    {
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

    [RelayCommand]
    public void ClearCustomSettings(VideoFile? video)
    {
        if (video != null)
        {
            video.ResetCustomSettings();
        }
    }

    [RelayCommand]
    public async Task AutoDownloadBinaries()
    {
        string targetFolder = AppPathService.FfmpegBinFolder;

            try
            {
                IsBusy = true;
                IsDownloading = true; 

                var progress = new Progress<string>(status => StatusMessage = status);

                await _systemService.InstallFfmpegAsync(targetFolder, progress);

                RefreshDependencyCheck();
                await _messageService.ShowSuccessAsync("Setup Complete", "FFmpeg has been downloaded and installed successfully!");
            }
            catch (Exception ex)
            {
                await _messageService.ShowErrorAsync("Download Failed", $"Could not auto-download FFmpeg.\n\nError: {ex.Message}\n\nPlease try the manual method.");
                StatusMessage = "Download failed.";
            }
            finally
            {
                IsBusy = false;
                IsDownloading = false;
            }
        
    }


    [RelayCommand]
    public async Task OpenSettings(Window owner)
    {
        var settingsVM = new SettingsViewModel(GlobalSettings);
        var settingsWin = new Views.SettingsWindow { DataContext = settingsVM };

        await settingsWin.ShowDialog(owner);

        var newSettings = settingsVM.GetUpdatedSettings();
        GlobalSettings = newSettings;

        OnPropertyChanged(nameof(AvailableEncoders));

        if (!AvailableEncoders.Contains(SelectedEncoder))
        {
            SelectedEncoder = AvailableEncoders.First();
        }

        if (settingsVM.SaveToDisk)
        {
            await _settingsService.SaveSettingsAsync(newSettings);
        }
    }

    [RelayCommand]
    public async Task OpenRenameWindow(Window owner)
    {
        var selected = Videos.Where(v => v.IsSelected).ToList();
        if (!selected.Any())
        {
            await _messageService.ShowInfoAsync("No Selection", "Please select files to rename first.");
            return;
        }

        var vm = new RenameViewModel(selected);
        var win = new Views.RenameWindow { DataContext = vm };
        await win.ShowDialog(owner);

        // Refresh the filtered list to show new names
        ApplyFilter();
    }

    [RelayCommand]
    public async Task LoadVideoDuration(VideoFile video)
    {
        if (video == null || video.IsDurationLoaded) return;

        try
        {
            double duration = await _ffprobeService.GetVideoDurationAsync(video.FilePath);
            if (duration > 0)
            {
                video.DurationSeconds = duration;
                video.StartTime = 0;
                video.EndTime = video.DurationSeconds;
                video.IsDurationLoaded = true;        
            }
        }
        catch (Exception)
        {
            await _messageService.ShowErrorAsync("Error", $"Could not read video duration");
        }
    }

    [RelayCommand]
    public void OpenFileFolder(VideoFile video)
    {
        if (video == null) return;

        try
        {
            string? directory = Path.GetDirectoryName(video.FilePath);
            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
            {
                _systemService.OpenLocalFolder(directory);
            }
        }
        catch (Exception)
        {
            // Log later
        }
    }

}

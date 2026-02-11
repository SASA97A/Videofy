using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Video_Size_Optimizer.Models;
using Video_Size_Optimizer.Services;
using Video_Size_Optimizer.Utils;

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
    private Views.LogWindow? _logWindow;
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
    [ObservableProperty] private int _splitSizeMb = 25;
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

        LogService.Instance.Section("Application Session Started" + DateTime.Now.ToString(" - yyyy.MM.dd"));
        LogService.Instance.Log("MainWindowViewModel initialized.");

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
            LogService.Instance.Log($"FFmpeg binaries missing. Expected path: {path}", LogLevel.Warning, "Warning");
            ShowDependencyWarning = true;
            ExpectedPath = path;
            StatusMessage = "Setup Required: Missing FFmpeg files.";
        }
        else
        {
            LogService.Instance.Log("FFmpeg binaries detected.");
        }

        GlobalSettings = _settingsService.LoadSettings();
        LogService.Instance.Log("Global settings loaded.");
    }

    // Check for updates
    [RelayCommand]
    public async Task CheckForUpdates()
    {
        LogService.Instance.Log("Checking for updates!");

        var latestVersion = await _systemService.GetLatestGithubTagNameAsync("SASA97A", "Videofy");

        if (latestVersion == null)
        {
            await _messageService.ShowErrorAsync("Check Failed", "Could not connect to GitHub!");
            return;
        }
        if (latestVersion != AppConstants.AppVersion)
        {
            LogService.Instance.Log($"New app version found {latestVersion}", LogLevel.Success);

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
        if (result == null || result.Count == 0)
        {
            LogService.Instance.Log("Folder picker cancelled by user.");
            return;
        }

        // 3. Convert the StorageFolder to a local path string
        var folderPath = result[0].TryGetLocalPath();

        if (string.IsNullOrEmpty(folderPath))
        {
            await _messageService.ShowErrorAsync("Folder Access Error", AppConstants.NoFolderAccess);
            return;
        }

        Videos.Clear();
        SelectedFolderPath = folderPath;

        LogService.Instance.Section("Folder Scan");

        var allowedExtensions = AppConstants.GetCombinedExtensions(GlobalSettings.CustomExtensions);
        LogService.Instance.Log(
              $"Readable extensions: {string.Join(", ", allowedExtensions)}");

        var data = _fileService.GetFolderData(folderPath, allowedExtensions);
        TotalFolderSizeDisplay = $"{(data.totalSize / 1024.0 / 1024.0 / 1024.0):F2} GB";

        LogService.Instance.Log(
                $"Path: {folderPath}");
        LogService.Instance.Log(
                $"Files found: {data.videoPaths.Count}");
        LogService.Instance.Log(
                $"Total size: {TotalFolderSizeDisplay}");

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
            LogService.Instance.Log("No videos selected. Batch aborted.");
            await _messageService.ShowInfoAsync("No Selection", AppConstants.NoSelectionMessage);
            return;
        }


        // Platform Permission Checks (Linux/macOS)
        LogService.Instance.Log("Checking FFmpeg permissions...");
        var ffmpegError = _ffmpegService.InitializePermissions();
        if (ffmpegError != null)
        {
            LogService.Instance.Log($"FFmpeg permission error: {ffmpegError}", LogLevel.Error, "Error");
            await _messageService.ShowErrorAsync("Platform Permission Error", ffmpegError);
            return;
        }

        LogService.Instance.Log("Checking FFprobe permissions...");
        var ffprobeError = _ffprobeService.InitializePermissions();
        if (ffprobeError != null)
        {
            LogService.Instance.Log($"FFprobe permission error: {ffprobeError}", LogLevel.Error, "Error");
            await _messageService.ShowErrorAsync("Platform Permission Error", ffprobeError);
            return;
        }

        int completedCount = 0;
        long totalBytesSaved = 0;

        IsBusy = true;
        StatusMessage = "Processing...";

        if (GlobalSettings.PreventSleep)
        {
            await _systemService.PreventSleepAsync(true, _messageService.ShowErrorAsync);
        }

        LogService.Instance.Section("Batch Start");

        LogService.Instance.Log($"Videos selected: {selectedVideos.Count}");
        LogService.Instance.Log($"Mode: {(SelectedTabIndex == 0 ? "Encode" : SelectedTabIndex == 1 ? "Stream Copy" : "Split")}");
        LogService.Instance.Log($"Encoder preset: {SelectedEncoder}");
        LogService.Instance.Log($"Default format: {GlobalSettings.DefaultOutputFormat}");
        LogService.Instance.Log($"Strip metadata: {StripMetadata}");
        LogService.Instance.Log($"Prevent sleep: {GlobalSettings.PreventSleep}");



        try
        {
            foreach (var video in selectedVideos)
            {
                if (!IsBusy)
                {
                    LogService.Instance.Log("Batch cancelled by user.");
                    break;
                }

                LogService.Instance.Section($"Processing video: {video.FileName}");

                if (IsDiskSpaceLow())
                {
                    LogService.Instance.Log("Low disk space detected. Pausing batch.", LogLevel.Warning, "Warning");
                    _ffmpegService.TogglePause(true);

                    if (!IsPaused) await TogglePause();
                    StatusMessage = "Paused: Low Disk Space!";
                    LogService.Instance.Log(
                                $"Pause reason: Low disk space (< {GlobalSettings.LowDiskBufferGb}GB)");

                    await _messageService.ShowErrorAsync("Low Disk Space",
                        $"Available space is below {GlobalSettings.LowDiskBufferGb}GB. " +
                        $"The process has been paused. Please free up space and click Resume.");

                    while (IsPaused)
                    {
                        if (!IsBusy)
                        { 
                            LogService.Instance.Log("User cancelled batch during low disk pause.");
                            break;
                        }                           
                        await Task.Delay(1000); 
                    }

                    if (!IsBusy)
                    {
                        LogService.Instance.Log("Batch cancelled by user.");
                        IsPaused = false;
                        break;
                    }
                }

                video.IsCompleted = false;
                video.Progress = 0;
                video.IsProcessing = true;

                await Task.Delay(50);

                LogService.Instance.SubSection("***Source info***");

                try
                {
                    if (video.DurationSeconds <= 0)
                    {                      
                        video.DurationSeconds = await _ffprobeService.GetVideoDurationAsync(video.FilePath);
                    }

                    long originalSizeBytes = new FileInfo(video.FilePath).Length;

                    // Get original width
                    int originalWidth = await _ffprobeService.GetVideoWidthAsync(video.FilePath);

                    LogService.Instance.Log(
                        $"Duration: {video.DurationSeconds:F2}s");
                    LogService.Instance.Log(
                        $"Original size: {(originalSizeBytes / 1024.0 / 1024.0):F2} MB");

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
                    LogService.Instance.SubSection("***Encoding settings***");

                    LogService.Instance.Log($"Resolution: {resolutionToUse}");

                    string fpsToUse = !string.IsNullOrEmpty(video.CustomFps)
                                       ? video.CustomFps : SelectedFps;

                    LogService.Instance.Log($"FPS: {fpsToUse}");

                    // Determine Encoder
                    if (!AppConstants.EncoderMap.TryGetValue(SelectedEncoder, out string? encoderValue))
                    {
                        encoderValue = "libx265";                       
                    }

                    LogService.Instance.Log($"Encoder: {encoderValue}");

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

                        LogService.Instance.Log(
                        $"Trimming enabled: {video.StartTime:F2}s → {video.EndTime:F2}s");
                    }

                    int? maxBitrate = null;
                    if (GlobalSettings.PreventUpsampling && video.DurationSeconds > 0)
                    {
                        double rawBitrate = ((originalSizeBytes * 8.0) / 1024.0) / video.DurationSeconds;
                        maxBitrate = (int)(rawBitrate);

                        //No cap under 100kbps or it will be unwatchable.
                        if (maxBitrate < 100) maxBitrate = null;

                        if (maxBitrate.HasValue)
                            LogService.Instance.Log($"Bitrate cap applied: {maxBitrate} kbps");
                    }

                    int videoIndex = completedCount + 1;
                    LogService.Instance.Section(
                            $"Processing ({videoIndex}/{selectedVideos.Count}): {video.FileName}");

                    if (SelectedTabIndex == 1)
                    {
                        //LogService.Instance.Log("[Main] Mode: Stream copy.");
                        finalOutputPath = _fileService.GenerateOutputPath(video.FilePath, 0, ConversionTargetFormat);
                        await _ffmpegService.CompressAsync(video.FilePath, finalOutputPath,
                            "Original", false, 0, "copy", "Original", trimArgs, null, p);

                        LogService.Instance.Log($"Output path: {finalOutputPath}");
                    }

                    else if (SelectedTabIndex == 2)
                    {
                        //LogService.Instance.Log($"[Main] Mode: Split video ({SplitSizeMb}MB parts).");

                        string originalExtension = Path.GetExtension(video.FilePath);
                        finalOutputPath = _fileService.GenerateSplitPatternPath(video.FilePath, originalExtension);
                        double segmentTime = CalculateSegmentTime(video.DurationSeconds, originalSizeBytes, SplitSizeMb);

                        LogService.Instance.Log($"Segment time calculated: {segmentTime:F2}s");

                        if (segmentTime > 0)
                        {
                            StatusMessage = "Splitting...";
                            string splitArgs = $"-f segment -segment_time {segmentTime.ToString(CultureInfo.InvariantCulture)} -reset_timestamps 1";  
                            
                            await _ffmpegService.SplitVideoAsync(video.FilePath, finalOutputPath, splitArgs, p);

                            video.IsCompleted = true;
                            video.Progress = 100;
                            completedCount++;

                            LogService.Instance.Log("Splitting completed.");
                            LogService.Instance.Log($"Output path: {finalOutputPath}");
                        }
                        else
                        {
                            // File is already smaller than the target split size
                            //video.StatusMessage = "Skipped (Small)";
                            LogService.Instance.Log("Split skipped: file already small.", LogLevel.Warning, "Warning");
                            video.IsCompleted = true;
                            video.Progress = 100;
                        }


                    }

                    else
                    {                      

                        finalOutputPath = video.HasCustomSize
                                            ? _fileService.GenerateTargetSizePath(video.FilePath, (int)video.CustomTargetSizeMb!, GlobalSettings.DefaultOutputFormat)
                                            : _fileService.GenerateOutputPath(video.FilePath, CrfValue, GlobalSettings.DefaultOutputFormat);
                                                                                                         

                        if (video.HasCustomSize)
                        {

                            LogService.Instance.Log(
                                        $"Target size mode: {video.CustomTargetSizeMb} MB (2-pass)",
                                        LogLevel.Info);

                            if (video.DurationSeconds > 0)
                            {
                                // 2. Run target size compression 

                                LogService.Instance.Log($"2-pass encoding started");

                                await _ffmpegService.CompressTargetSizeAsync(video.FilePath,
                                    finalOutputPath, fpsToUse, StripMetadata,
                                    (int)video.CustomTargetSizeMb!, encoderValue,
                                    resolutionToUse, video.DurationSeconds, trimArgs, p);
                            }
                        }
                        else
                        {
                            LogService.Instance.Log(
                                        $"CRF mode: CRF={CrfValue}",
                                        LogLevel.Info);
                            // Standard Batch Process - Uses Global CRF
                            await _ffmpegService.CompressAsync(video.FilePath, finalOutputPath,
                                fpsToUse, StripMetadata, CrfValue, encoderValue, resolutionToUse, trimArgs, maxBitrate, p);
                        }

                        LogService.Instance.Log($"Output path: {finalOutputPath}");
                    }
                 
                    // Important to avoid deleteing files if user Stops compression!
                    if (!IsBusy && !video.IsSplitEnabled)
                    {                      
                        if (File.Exists(finalOutputPath)) File.Delete(finalOutputPath);
                        break;
                    }

                    if (File.Exists(finalOutputPath))
                    {
                        var newInfo = new FileInfo(finalOutputPath);
                        long totalOutputSize = newInfo.Length;

                        if (video.IsSplitEnabled)
                        {
                            totalOutputSize = video.HasCustomSize
                                ? (long)video.CustomTargetSizeMb! * 1024 * 1024
                                : originalSizeBytes;
                        }

                        if (totalOutputSize != 0)
                        {
                            totalBytesSaved += (originalSizeBytes - totalOutputSize);
                            completedCount++;

                            double newSize = totalOutputSize / 1024.0 / 1024.0;
                            video.UpdateStatusSize(newSize);

                            video.Progress = 100;
                            video.IsCompleted = true;

                            LogService.Instance.Log(
                                        $"Completed | New size: {newSize:F2} MB",
                                        LogLevel.Success);

                            var index = DisplayedVideos.IndexOf(video);
                            if (index != -1)
                            {
                                // This "re-seats" the item in the collection, forcing the row to refresh
                                DisplayedVideos[index] = video;
                            }

                            if (SelectedTabIndex != 1 && GlobalSettings.DeleteOriginalAfterCompression && File.Exists(finalOutputPath) && !video.IsSplitEnabled)
                            {
                                try
                                {
                                    File.Delete(video.FilePath);
                                }
                                catch (Exception ex)
                                {
                                    LogService.Instance.Log(
                                        $"Video deletion failed: {video.FileName} | {ex.Message}", LogLevel.Error, "Error");
                                }
                            }
                        }                                                   
                    }                                     
                }
                catch (Exception ex)
                {
                    LogService.Instance.Log(
                    $"Video processing failed | File={video.FileName} | Mode={SelectedTabIndex}" +
                    $" | Error={ex}", LogLevel.Error, "Error");

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

                LogService.Instance.Section("Batch Completed");

                LogService.Instance.Log($"Videos processed: {completedCount}");
                LogService.Instance.Log($"Space saved: {sizeDisplay}", LogLevel.Success);

                await _messageService.ShowSuccessAsync("Task Completed",
                       $"Successfully processed {completedCount} videos.\n\nTotal space saved: {sizeDisplay}");
               }
        }
        finally
        {
            await _systemService.PreventSleepAsync(false);

            LogService.Instance.Log("System sleep prevention disabled.");
            LogService.Instance.Log("Batch finished. Application idle.");

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

            LogService.Instance.Log(
                $"Drive={drive.Name} | Free={drive.AvailableFreeSpace / 1024 / 1024 / 1024}GB | Buffer={GlobalSettings.LowDiskBufferGb}GB");

            return drive.AvailableFreeSpace < bufferBytes;
        }
        catch (Exception ex) 
        {
            LogService.Instance.Log($"There was an error while checking disk space: {ex.Message}", LogLevel.Error, "ERROR");
            return false;
        } 
    }

    //Returns calculatd segement size
    private double CalculateSegmentTime(double duration, long originalSizeBytes, int targetSplitMb)
    {
        if (duration <= 0 || originalSizeBytes <= 0 || targetSplitMb <= 0) return 0;

        double splitSizeBytes = targetSplitMb * 1024.0 * 1024.0 * 0.9;

        if (splitSizeBytes >= originalSizeBytes) return 0;

        // Math: SegmentDuration = TotalDuration * (TargetSplit / TotalSize)
        return duration * (splitSizeBytes / originalSizeBytes);
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

        LogService.Instance.Log("StopAllProcessing invoked.");

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
            LogService.Instance.Log("Processing force-stopped by user.");
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
            ApplyFilter();
            RefreshStats();
        }
    }

    [RelayCommand]
    private async Task TogglePause()
    {
        LogService.Instance.Log($"TogglePause requested. CurrentPaused={IsPaused}");

        if (IsPaused)
        {
            if (IsDiskSpaceLow())
            {
                LogService.Instance.Log("Resume blocked due to low disk space.");

                await _messageService.ShowErrorAsync("Resume Blocked",
                    $"Still low on disk space (under {GlobalSettings.LowDiskBufferGb}GB). Please free up space before resuming.");
                return;
            }
        }

        IsPaused = !IsPaused;
        _ffmpegService.TogglePause(IsPaused);
        LogService.Instance.Log($"Pause state changed. IsPaused={IsPaused}");

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
            LogService.Instance.Log($"Could not prepare or open the folder: {ex.Message}", LogLevel.Error, "Error");
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

        LogService.Instance.Log($"Auto-download started. Target={targetFolder}");

        try
        {
            IsBusy = true;
            IsDownloading = true;

            var progress = new Progress<string>(status => StatusMessage = status);

            await _systemService.InstallFfmpegAsync(targetFolder, progress);

            RefreshDependencyCheck();
            await _messageService.ShowSuccessAsync("Setup Complete", "FFmpeg has been downloaded and installed successfully!");

            LogService.Instance.Log("FFmpeg download & installation completed.", LogLevel.Success);

        }

        catch (Exception ex)
        {
            LogService.Instance.Log($"There was an error during auto-download: {ex.Message}", LogLevel.Error, "Error");
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
            LogService.Instance.Log("Settings saved to disk.");
        }
        else
        {
            LogService.Instance.Log("Settings applied without saving.");
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
        catch (Exception ex)
        {
            LogService.Instance.Log($"Could not load video duration: {ex.Message}", LogLevel.Error, "Error");
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
        catch (Exception ex)
        {
            LogService.Instance.Log($"There was an error while opening file folder: {ex.Message}", LogLevel.Error, "Error");
        }
    }


    [RelayCommand]
    public void OpenLogFile()
    {
        // Opens the logs.txt in Notepad/Default Editor
        string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Videofy");
        string logFile = Path.Combine(folder, "app_logs.txt");
        if (File.Exists(logFile))
        {
            Process.Start(new ProcessStartInfo(logFile) { UseShellExecute = true });
        }
    }

    [RelayCommand]
    public void ShowLogs(Window parent)
    {
        if (_logWindow != null)
        {
            _logWindow.Activate(); 
            return;
        }

        _logWindow = new Views.LogWindow();
        _logWindow.Closed += (s, e) => _logWindow = null; // Reset when closed
        _logWindow.Show(parent);
    }

}

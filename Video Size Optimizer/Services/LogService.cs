using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace Video_Size_Optimizer.Services;

public enum LogLevel
{
    Info,
    Success,
    Warning,
    Error,
    Debug
}

public class LogEntry
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public LogLevel Level { get; set; } = LogLevel.Info;
    public string Scope { get; set; } = "Main";
    public string Message { get; set; } = string.Empty;
    public bool IsSectionHeader { get; set; }
    public bool IsSubSection { get; set; }

    public string FormattedTimestamp => $"[{Timestamp:HH:mm:ss}]";

    public string LevelTag => Level switch
    {
        LogLevel.Success => "[SUCCESS]",
        LogLevel.Warning => "[WARN]",
        LogLevel.Error => "[ERROR]",
        LogLevel.Debug => "[DEBUG]",
        _ => "[INFO]"
    };

    public string LevelColorHex => Level switch
    {
        LogLevel.Error => "#FF5555",
        LogLevel.Warning => "#FFA726",
        LogLevel.Success => "#66BB6A",
        LogLevel.Debug => "#888888",
        _ => "#E0E0E0"
    };

    public string FormattedText => IsSectionHeader
        ? $"============================================================\n=== {Message} ===\n============================================================"
        : $"{FormattedTimestamp} {LevelTag,-9} [{Scope}] {Message}";
}

public partial class LogService : ObservableObject
{
    private static readonly LogService _instance = new();
    public static LogService Instance => _instance;

    public ObservableCollection<LogEntry> LogEntries { get; } = new();
    public ObservableCollection<LogEntry> FilteredLogEntries { get; } = new();

    [ObservableProperty]
    private int selectedFilterIndex = 0;

    [ObservableProperty]
    private string logFileSizeDisplay = "Log size: 0 KB";

    // The log file is never trimmed or renamed by the app. Above this size the user is told
    // (Log window note and a popup at startup) so they can delete it themselves.
    public const long LargeLogFileBytes = 10L * 1024 * 1024;

    [ObservableProperty]
    private bool isLogFileLarge;

    public string LogFilePath => _logFilePath;
    public string LargeLogFileNote =>
        $"The log file is over {LargeLogFileBytes / 1024 / 1024} MB. Consider deleting it: {_logFilePath}";

    private readonly string _logFilePath;

    // Log() is called from several threads (UI, folder scan, ffmpeg). Without a lock, two
    // writes at the same moment fail with "file in use" and the line is lost silently.
    private readonly object _fileLock = new();
    // Size kept in memory so each log line does not read the file size from disk.
    private long _logFileBytes;

    private LogService()
    {
        string folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Videofy");

        Directory.CreateDirectory(folder);
        _logFilePath = Path.Combine(folder, "app_logs.txt");

        // No dispatcher here: the service can be created before the UI thread is running.
        try { _logFileBytes = File.Exists(_logFilePath) ? new FileInfo(_logFilePath).Length : 0; } catch { }
        UpdateLogFileSizeDisplay();
    }

    partial void OnSelectedFilterIndexChanged(int value)
    {
        FilteredLogEntries.Clear();
        foreach (var entry in LogEntries)
        {
            if (PassesFilter(entry, value))
            {
                FilteredLogEntries.Add(entry);
            }
        }
    }

    private static bool PassesFilter(LogEntry entry, int filterIndex) => filterIndex switch
    {
        1 => entry.Level == LogLevel.Error,
        2 => entry.Level == LogLevel.Error || entry.Level == LogLevel.Warning,
        _ => true
    };

    public void Log(string message, LogLevel level = LogLevel.Info, string scope = "Main", bool isSectionHeader = false, bool isSubSection = false)
    {
        var entry = new LogEntry
        {
            Timestamp = DateTime.Now,
            Level = level,
            Scope = scope,
            Message = message,
            IsSectionHeader = isSectionHeader,
            IsSubSection = isSubSection
        };

        Dispatcher.UIThread.Post(() =>
        {
            LogEntries.Add(entry);
            if (LogEntries.Count > 1000)
                LogEntries.RemoveAt(0);

            if (PassesFilter(entry, SelectedFilterIndex))
            {
                FilteredLogEntries.Add(entry);
                if (FilteredLogEntries.Count > 1000)
                    FilteredLogEntries.RemoveAt(0);
            }

        });

        string line = entry.FormattedText + Environment.NewLine;
        try
        {
            lock (_fileLock)
            {
                File.AppendAllText(_logFilePath, line);
                _logFileBytes += Encoding.UTF8.GetByteCount(line);
            }
        }
        catch { }

        Dispatcher.UIThread.Post(UpdateLogFileSizeDisplay);
    }

    public void Section(string title)
    {
        Log("============================================================", LogLevel.Info, "Main");
        Log($"=== {title.ToUpperInvariant()} ===", LogLevel.Success, "Main", isSectionHeader: true);
        Log("============================================================", LogLevel.Info, "Main");
    }

    public void SubSection(string title)
    {
        Log($"--- {title} ---", LogLevel.Debug, "Main", isSubSection: true);
    }

    public void ClearLogs()
    {
        Dispatcher.UIThread.Post(() =>
        {
            LogEntries.Clear();
            FilteredLogEntries.Clear();
        });
    }

    public string GetLogsAsText()
    {
        return string.Join(Environment.NewLine, FilteredLogEntries.Select(e => e.FormattedText));
    }

    /// <summary>
    /// Deletes the log file on the user's request. The next log line starts a new file.
    /// Returns null on success, or the error message.
    /// </summary>
    public string? DeleteLogFile()
    {
        try
        {
            lock (_fileLock)
            {
                if (File.Exists(_logFilePath)) File.Delete(_logFilePath);
                _logFileBytes = 0;
            }
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
        finally
        {
            RefreshLogFileSize();
        }

        Log("Log file deleted by user.", LogLevel.Info, "Log");
        return null;
    }

    /// <summary>
    /// Reads the real file size from disk (when the Log window opens and before the startup note),
    /// in case the user deleted or edited the file while the app was running.
    /// </summary>
    public void RefreshLogFileSize()
    {
        try
        {
            lock (_fileLock)
            {
                _logFileBytes = File.Exists(_logFilePath) ? new FileInfo(_logFilePath).Length : 0;
            }
        }
        catch
        {
            // Keep the last known size.
        }

        if (Dispatcher.UIThread.CheckAccess())
            UpdateLogFileSizeDisplay();
        else
            Dispatcher.UIThread.Post(UpdateLogFileSizeDisplay);
    }

    private void UpdateLogFileSizeDisplay()
    {
        long bytes = Interlocked.Read(ref _logFileBytes);

        LogFileSizeDisplay = bytes switch
        {
            < 1024 * 1024 =>
                $"Log size: {bytes / 1024.0:F1} KB",

            _ =>
                $"Log size: {bytes / 1024.0 / 1024.0:F2} MB"
        };

        IsLogFileLarge = bytes >= LargeLogFileBytes;
    }
}

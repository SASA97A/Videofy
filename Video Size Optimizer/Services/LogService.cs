using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

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
    public ObservableCollection<string> LogLines { get; } = new();

    [ObservableProperty]
    private int selectedFilterIndex = 0;

    [ObservableProperty]
    private string fullLogText = string.Empty;

    [ObservableProperty]
    private string logFileSizeDisplay = "Log size: 0 KB";

    private readonly string _logFilePath;

    private LogService()
    {
        string folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Videofy");

        Directory.CreateDirectory(folder);
        _logFilePath = Path.Combine(folder, "app_logs.txt");

        UpdateLogFileSize();
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

            LogLines.Add(entry.FormattedText);
            if (LogLines.Count > 1000)
                LogLines.RemoveAt(0);

            FullLogText = string.Join(Environment.NewLine, LogLines);
        });

        try
        {
            File.AppendAllText(_logFilePath, entry.FormattedText + Environment.NewLine);
        }
        catch { }

        Dispatcher.UIThread.Post(UpdateLogFileSize);
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
            LogLines.Clear();
            FullLogText = string.Empty;
        });
    }

    public string GetLogsAsText()
    {
        return string.Join(Environment.NewLine, FilteredLogEntries.Select(e => e.FormattedText));
    }

    private void UpdateLogFileSize()
    {
        try
        {
            if (!File.Exists(_logFilePath))
            {
                LogFileSizeDisplay = "Log size: 0 KB";
                return;
            }

            long bytes = new FileInfo(_logFilePath).Length;

            string display = bytes switch
            {
                < 1024 * 1024 =>
                    $"Log size: {bytes / 1024.0:F1} KB",

                _ =>
                    $"Log size: {bytes / 1024.0 / 1024.0:F2} MB"
            };

            LogFileSizeDisplay = display;
        }
        catch
        {
            LogFileSizeDisplay = "Log size: ?";
        }
    }
}

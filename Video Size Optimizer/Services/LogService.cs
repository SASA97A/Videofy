using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;
using System.IO;

namespace Video_Size_Optimizer.Services;

public enum LogLevel
{
    Info,
    Success,
    Warning,
    Error,
    Debug
}

public partial class LogService : ObservableObject
{
    private static readonly LogService _instance = new();
    public static LogService Instance => _instance;

    public ObservableCollection<string> LogLines { get; } = new();

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

    public void Log( string message, LogLevel level = LogLevel.Info,
                     string scope = "Main")
    {
        string prefix = level switch
        {
            LogLevel.Success => "✔",
            LogLevel.Warning => "⚠",
            LogLevel.Error => "✖",
            LogLevel.Debug => "…",
            _ => "•"
        };

        string timestampedMessage =
            $"[{DateTime.Now:HH:mm:ss}] {prefix} [{scope}] {message}";

        Dispatcher.UIThread.Post(() =>
        {
            LogLines.Add(timestampedMessage);

            if (LogLines.Count > 1000)
                LogLines.RemoveAt(0);

            FullLogText = string.Join(Environment.NewLine, LogLines);
        });

        try
        {
            File.AppendAllText(_logFilePath, timestampedMessage + Environment.NewLine);
        }
        catch { }

        Dispatcher.UIThread.Post(UpdateLogFileSize);

    }

    public void Section(string title)
    {
        Log(new string('─', 60));
        Log(title.ToUpperInvariant(), LogLevel.Success);
        Log(new string('─', 60));
    }

    public void SubSection(string title)
    {
        Log(title, LogLevel.Debug);
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
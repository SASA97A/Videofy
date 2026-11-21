using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Threading;
using MsBox.Avalonia;
using MsBox.Avalonia.Dto;
using MsBox.Avalonia.Enums;
using System.Collections.Specialized;
using Video_Size_Optimizer.Services;

namespace Video_Size_Optimizer.Views;

public partial class LogWindow : Window
{
    public LogWindow()
    {
        InitializeComponent();
        DataContext = LogService.Instance;
        // The user may have deleted the file while the app was running.
        LogService.Instance.RefreshLogFileSize();

        LogService.Instance.FilteredLogEntries.CollectionChanged += OnFilteredLogEntriesChanged;
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        LogService.Instance.FilteredLogEntries.CollectionChanged -= OnFilteredLogEntriesChanged;
    }

    private void OnFilteredLogEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var count = LogService.Instance.FilteredLogEntries.Count;
            if (count > 0 && LogListBox != null)
            {
                LogListBox.ScrollIntoView(count - 1);
            }
        });
    }

    private async void CopyLogsClick(object sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.Clipboard != null)
        {
            string text = LogService.Instance.GetLogsAsText();
            await topLevel.Clipboard.SetTextAsync(text);
        }
    }

    private void ClearLogsClick(object sender, RoutedEventArgs e)
    {
        LogService.Instance.ClearLogs();
    }

    private async void DeleteLogFileClick(object sender, RoutedEventArgs e)
    {
        var log = LogService.Instance;
        log.RefreshLogFileSize();

        // Dialogs are owned by this window so they open over it, not over the main window.
        var confirm = MessageBoxManager.GetMessageBoxStandard(new MessageBoxStandardParams
        {
            ContentTitle = "Delete Log File?",
            ContentMessage = $"Delete the log file? ({log.LogFileSizeDisplay.Replace("Log size: ", "")})\n\n{log.LogFilePath}\n\n" +
                             "Past log history will be lost. A new log file is started automatically.  ",
            ButtonDefinitions = ButtonEnum.YesNo,
            Icon = MsBox.Avalonia.Enums.Icon.Question,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        });
        if (await confirm.ShowWindowDialogAsync(this) != ButtonResult.Yes) return;

        string? error = log.DeleteLogFile();
        if (error != null)
        {
            var failed = MessageBoxManager.GetMessageBoxStandard(new MessageBoxStandardParams
            {
                ContentTitle = "Delete Failed",
                ContentMessage = $"The log file could not be deleted.\n\n{error}  ",
                ButtonDefinitions = ButtonEnum.Ok,
                Icon = MsBox.Avalonia.Enums.Icon.Error,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            });
            await failed.ShowWindowDialogAsync(this);
        }
    }

    private void CloseClick(object sender, RoutedEventArgs e) => Close();
}
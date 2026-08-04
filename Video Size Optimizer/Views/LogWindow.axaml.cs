using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using System.Collections.Specialized;
using Video_Size_Optimizer.Services;

namespace Video_Size_Optimizer.Views;

public partial class LogWindow : Window
{
    public LogWindow()
    {
        InitializeComponent();
        DataContext = LogService.Instance;

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

    private void CloseClick(object sender, RoutedEventArgs e) => Close();
}
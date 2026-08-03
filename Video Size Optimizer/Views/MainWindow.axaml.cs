using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using MsBox.Avalonia;
using MsBox.Avalonia.Dto;
using MsBox.Avalonia.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Video_Size_Optimizer.ViewModels;

namespace Video_Size_Optimizer.Views;

public partial class MainWindow : Window
{
    private readonly MessageService _messageService = new();

    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        if (e.Data.Contains(DataFormats.Files) || e.Data.GetFiles() != null)
        {
            e.DragEffects = DragDropEffects.Copy;
            DragDropOverlay.IsVisible = true;
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
        }
    }

    private void OnDragLeave(object? sender, DragEventArgs e)
    {
        DragDropOverlay.IsVisible = false;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        DragDropOverlay.IsVisible = false;

        var items = e.Data.GetFiles();
        if (items == null) return;

        var paths = items
            .Select(item => item.TryGetLocalPath())
            .Where(path => !string.IsNullOrEmpty(path))
            .Cast<string>()
            .ToList();

        if (paths.Count > 0 && DataContext is MainWindowViewModel vm)
        {
            await vm.AddPathsAsync(paths);
        }
    }

    private void OnExitClick(object sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close();
    }

    protected override async void OnClosing(WindowClosingEventArgs e)
    {
        var vm = DataContext as MainWindowViewModel;
        if (vm == null) return;

        // If VM is not busy, just close
        if (vm.RequestClose())
        {
            base.OnClosing(e);
            return;
        }

        // VM is busy, cancel the initial close request
        e.Cancel = true;

        bool shouldExit = await _messageService.ShowYesNoAsync(
            "Active Encoding",
            "A video is currently being processed. If you exit now, the file will be corrupted.\n\nStop encoding and exit?");

        if (shouldExit)
        {
            // Stop processing
            await vm.StopAllProcessing(true);
            Close();
        }
    }
}
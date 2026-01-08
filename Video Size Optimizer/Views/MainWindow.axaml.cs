using Avalonia.Controls;
using MsBox.Avalonia;
using MsBox.Avalonia.Dto;
using MsBox.Avalonia.Enums;
using System;
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
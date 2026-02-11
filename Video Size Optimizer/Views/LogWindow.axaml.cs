using Avalonia.Controls;
using Avalonia.Interactivity;
using System.ComponentModel;
using Video_Size_Optimizer.Services;

namespace Video_Size_Optimizer.Views;

public partial class LogWindow : Window
{
    public LogWindow()
    {
        InitializeComponent();
        DataContext = LogService.Instance;      
    }

    private void CloseClick(object sender, RoutedEventArgs e) => Close();
    
}
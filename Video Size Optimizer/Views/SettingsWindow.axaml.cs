using Avalonia.Controls;
using Avalonia.Interactivity;


namespace Video_Size_Optimizer.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
    }

    public void OnSaveClick(object sender, RoutedEventArgs e)
    {
        this.Close();
    }
}
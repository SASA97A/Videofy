using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Video_Size_Optimizer.Views;

public partial class MergeGroupsWindow : Window
{
    public MergeGroupsWindow()
    {
        InitializeComponent();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}

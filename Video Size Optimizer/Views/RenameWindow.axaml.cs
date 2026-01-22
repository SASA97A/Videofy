using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Video_Size_Optimizer.ViewModels;

namespace Video_Size_Optimizer.Views;

public partial class RenameWindow : Window
{
    public RenameWindow()
    {
        InitializeComponent();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) => Close();

    private void OnApplyClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is RenameViewModel vm)
        {
            vm.ApplyRename();
            Close();
        }
    }
}
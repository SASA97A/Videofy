using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Video_Size_Optimizer.Converters;

public static class BoolConverters
{
    public static readonly IValueConverter ToOpacity =
        new FuncValueConverter<bool, double>(b => b ? 1.0 : 0.0);


    // Returns Green when active, Transparent when inactive
    public static readonly IValueConverter ModeToColorConverter =
        new FuncValueConverter<bool, IBrush>(b =>
            b ? Brush.Parse("#00d26a") : Brushes.Transparent);

    // Returns Black text when on Green background, Gray when inactive
    public static readonly IValueConverter ModeToTextConverter =
        new FuncValueConverter<bool, IBrush>(b =>
            b ? Brushes.Black : Brushes.Gray);

    // New converter for the Rename Window
    public static readonly IValueConverter IndexToVisible =
        new FuncValueConverter<int, bool>(i => i == 0);

    // A more flexible version for multiple modes:
    public static bool IsEqual(int value, int parameter) => value == parameter;
}

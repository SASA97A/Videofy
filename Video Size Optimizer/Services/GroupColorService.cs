using Avalonia.Media;

namespace Video_Size_Optimizer.Services;

public static class GroupColorService
{
    private static readonly (IBrush Background, IBrush Foreground)[] Palette = new[]
    {
        (Brush.Parse("#2E7D32"), Brush.Parse("#FFFFFF")), // G1: Dark Forest Green
        (Brush.Parse("#1565C0"), Brush.Parse("#FFFFFF")), // G2: Cobalt Blue
        (Brush.Parse("#D84315"), Brush.Parse("#FFFFFF")), // G3: Deep Terracotta
        (Brush.Parse("#6A1B9A"), Brush.Parse("#FFFFFF")), // G4: Purple
        (Brush.Parse("#00838F"), Brush.Parse("#FFFFFF")), // G5: Teal
        (Brush.Parse("#C2185B"), Brush.Parse("#FFFFFF")), // G6: Magenta
        (Brush.Parse("#E65100"), Brush.Parse("#FFFFFF")), // G7: Burnt Orange
        (Brush.Parse("#283593"), Brush.Parse("#FFFFFF")), // G8: Indigo
        (Brush.Parse("#00695C"), Brush.Parse("#FFFFFF")), // G9: Dark Cyan
        (Brush.Parse("#AD1457"), Brush.Parse("#FFFFFF")), // G10: Berry Red
        (Brush.Parse("#F57F17"), Brush.Parse("#000000")), // G11: Amber Gold
        (Brush.Parse("#37474F"), Brush.Parse("#FFFFFF")), // G12: Slate
        (Brush.Parse("#8E24AA"), Brush.Parse("#FFFFFF")), // G13: Bright Violet
        (Brush.Parse("#00897B"), Brush.Parse("#FFFFFF")), // G14: Mint Teal
        (Brush.Parse("#D81B60"), Brush.Parse("#FFFFFF")), // G15: Rose Red
        (Brush.Parse("#1E88E5"), Brush.Parse("#FFFFFF")), // G16: Bright Blue
        (Brush.Parse("#43A047"), Brush.Parse("#000000")), // G17: Leaf Green
        (Brush.Parse("#FB8C00"), Brush.Parse("#000000")), // G18: Warm Amber
        (Brush.Parse("#5E35B1"), Brush.Parse("#FFFFFF")), // G19: Royal Purple
        (Brush.Parse("#00ACC1"), Brush.Parse("#000000")), // G20: Bright Cyan
        (Brush.Parse("#E53935"), Brush.Parse("#FFFFFF")), // G21: Crimson
        (Brush.Parse("#7CB342"), Brush.Parse("#000000")), // G22: Lime Green
        (Brush.Parse("#FDD835"), Brush.Parse("#000000")), // G23: Bright Yellow
        (Brush.Parse("#3949AB"), Brush.Parse("#FFFFFF")), // G24: Deep Blue
        (Brush.Parse("#5D4037"), Brush.Parse("#FFFFFF")), // G25: Bronze Brown
        (Brush.Parse("#0097A7"), Brush.Parse("#FFFFFF")), // G26: Deep Teal
        (Brush.Parse("#8E24AA"), Brush.Parse("#FFFFFF")), // G27: Deep Violet
        (Brush.Parse("#C0392B"), Brush.Parse("#FFFFFF")), // G28: Dark Red
        (Brush.Parse("#16A085"), Brush.Parse("#FFFFFF")), // G29: Emerald Cyan
        (Brush.Parse("#2C3E50"), Brush.Parse("#FFFFFF"))  // G30: Midnight Blue
    };

    public static (IBrush Background, IBrush Foreground) GetGroupColor(int? groupNumber)
    {
        if (!groupNumber.HasValue || groupNumber.Value < 1)
            return (Brush.Parse("#333333"), Brush.Parse("#FFFFFF"));

        int index = (groupNumber.Value - 1) % Palette.Length;
        return Palette[index];
    }
}

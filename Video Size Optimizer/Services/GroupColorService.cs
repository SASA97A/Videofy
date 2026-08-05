using Avalonia.Media;

namespace Video_Size_Optimizer.Services;

public static class GroupColorService
{
    private static readonly (IBrush Background, IBrush Foreground)[] Palette = new[]
    {
        (Brush.Parse("#2E7D32"), Brush.Parse("#FFFFFF")), // G1: Forest Emerald
        (Brush.Parse("#1565C0"), Brush.Parse("#FFFFFF")), // G2: Ocean Blue
        (Brush.Parse("#D84315"), Brush.Parse("#FFFFFF")), // G3: Rust Terracotta
        (Brush.Parse("#7B1FA2"), Brush.Parse("#FFFFFF")), // G4: Amethyst Purple
        (Brush.Parse("#FBC02D"), Brush.Parse("#000000")), // G5: Sunburst Gold
        (Brush.Parse("#00838F"), Brush.Parse("#FFFFFF")), // G6: Deep Teal
        (Brush.Parse("#E91E63"), Brush.Parse("#FFFFFF")), // G7: Hot Pink / Magenta
        (Brush.Parse("#3E2723"), Brush.Parse("#FFFFFF")), // G8: Espresso Brown
        (Brush.Parse("#00E676"), Brush.Parse("#000000")), // G9: Electric Lime
        (Brush.Parse("#651FFF"), Brush.Parse("#FFFFFF")), // G10: Electric Indigo
        (Brush.Parse("#FF9100"), Brush.Parse("#000000")), // G11: Tangerine Orange
        (Brush.Parse("#00E5FF"), Brush.Parse("#000000")), // G12: Neon Aqua/Cyan
        (Brush.Parse("#8D6E63"), Brush.Parse("#FFFFFF")), // G13: Warm Taupe Slate
        (Brush.Parse("#B71C1C"), Brush.Parse("#FFFFFF")), // G14: Dark Blood Crimson
        (Brush.Parse("#CDDC39"), Brush.Parse("#000000")), // G15: Acid Lime
        (Brush.Parse("#0288D1"), Brush.Parse("#FFFFFF")), // G16: Sky Blue
        (Brush.Parse("#1B5E20"), Brush.Parse("#FFFFFF")), // G17: Dark Jungle Pine
        (Brush.Parse("#26A69A"), Brush.Parse("#000000")), // G18: Seafoam Mint Green
        (Brush.Parse("#4A148C"), Brush.Parse("#FFFFFF")), // G19: Deep Imperial Purple
        (Brush.Parse("#1DE9B6"), Brush.Parse("#000000")), // G20: Turquoise Mint
        (Brush.Parse("#FF6D00"), Brush.Parse("#000000")), // G21: Flame Orange
        (Brush.Parse("#37474F"), Brush.Parse("#FFFFFF")), // G22: Slate Steel Blue
        (Brush.Parse("#9E9D24"), Brush.Parse("#000000")), // G23: Olive Gold
        (Brush.Parse("#004D40"), Brush.Parse("#FFFFFF")), // G24: Dark Emerald Teal
        (Brush.Parse("#D500F9"), Brush.Parse("#FFFFFF")), // G25: Neon Violet Orchid
        (Brush.Parse("#2979FF"), Brush.Parse("#FFFFFF")), // G26: Royal Cobalt
        (Brush.Parse("#FFD600"), Brush.Parse("#000000")), // G27: Lemon Yellow
        (Brush.Parse("#78909C"), Brush.Parse("#000000")), // G28: Cool Slate Blue
        (Brush.Parse("#FF5252"), Brush.Parse("#000000")), // G29: Coral Red
        (Brush.Parse("#00B0FF"), Brush.Parse("#000000"))  // G30: Electric Sky
    };

    public static (IBrush Background, IBrush Foreground) GetGroupColor(int? groupNumber)
    {
        if (!groupNumber.HasValue || groupNumber.Value < 1)
            return (Brush.Parse("#333333"), Brush.Parse("#FFFFFF"));

        int index = (groupNumber.Value - 1) % Palette.Length;
        return Palette[index];
    }
}

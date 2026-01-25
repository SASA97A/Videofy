using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace Video_Size_Optimizer.Converters;

public class TimeSpanToSecondsConverter : IValueConverter
{
    // Converts Seconds (double) to String (00:00:00.000) for the UI
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double seconds)
        {
            var t = TimeSpan.FromSeconds(seconds);
            return t.ToString(@"hh\:mm\:ss\.fff");
        }
        return "00:00:00.000";
    }

    // Converts String (00:00:00.000) back to Seconds (double) for the Model
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string text && TimeSpan.TryParse(text, out TimeSpan result))
        {
            return result.TotalSeconds;
        }
        return 0.0;
    }
}

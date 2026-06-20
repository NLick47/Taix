using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data.Converters;

namespace Taix.Client.Controls.Converters;

public class PercentToWidthConverter : IValueConverter
{
    public static readonly PercentToWidthConverter Instance = new();

    public const double MaxPixels = 220.0;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double d) return Math.Max(0, Math.Min(d, 100)) / 100.0 * MaxPixels;
        if (value is float f) return Math.Max(0, Math.Min(f, 100)) / 100.0 * MaxPixels;
        if (value is int i) return Math.Max(0, Math.Min(i, 100)) / 100.0 * MaxPixels;
        return 0.0;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

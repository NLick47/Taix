using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Taix.Client.Controls.Converters;

public class CategoryColorBrushConverter : IValueConverter
{
    public static readonly CategoryColorBrushConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string s && !string.IsNullOrWhiteSpace(s))
        {
            try
            {
                return new SolidColorBrush(Color.Parse(s));
            }
            catch
            {
                // 颜色不合法，落到 fallback
            }
        }
        if (parameter is string fallback && !string.IsNullOrWhiteSpace(fallback))
        {
            try
            {
                return new SolidColorBrush(Color.Parse(fallback));
            }
            catch
            {
                // 忽略
            }
        }
        return Brushes.Transparent;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

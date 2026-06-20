using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Taix.Client.Shared.Librarys;

namespace Taix.Client.Controls.Converters;

public class SecondsToTextConverter : IValueConverter
{
    public static readonly SecondsToTextConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        long secs = value switch
        {
            long l => l,
            int i => i,
            double d => (long)d,
            _ => 0,
        };
        if (secs <= 0) return string.Empty;
        return Time.ToString((int)Math.Min(secs, int.MaxValue));
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

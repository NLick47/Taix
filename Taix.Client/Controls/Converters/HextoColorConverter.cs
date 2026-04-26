using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Taix.Client.Base.Color;

namespace Taix.Client.Controls.Converters;

public class HextoColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null) return null;
        return Colors.GetFromString(value.ToString());
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
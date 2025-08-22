using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Core.Librarys;

namespace UI.Controls.Converters;

public class TimeConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return Time.ToString(int.Parse(value.ToString()));
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
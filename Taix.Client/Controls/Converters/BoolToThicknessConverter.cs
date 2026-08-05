using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace Taix.Client.Controls.Converters;

public class BoolToThicknessConverter : IValueConverter
{
    public Thickness TrueValue { get; set; }
    public Thickness FalseValue { get; set; }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && b ? TrueValue : FalseValue;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
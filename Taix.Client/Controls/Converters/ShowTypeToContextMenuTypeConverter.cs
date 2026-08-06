using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Taix.Client.Controls.Charts;

namespace Taix.Client.Controls.Converters;

public class ShowTypeToContextMenuTypeConverter : IValueConverter
{
    public static readonly ShowTypeToContextMenuTypeConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is int id && id == 1 ? ContextMenuType.WebSite : ContextMenuType.App;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

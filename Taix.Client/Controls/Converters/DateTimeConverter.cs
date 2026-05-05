using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Taix.Client.Controls.Converters;

public class DateTimeConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (!(value is DateTime)) return "参数错误";
        var dateTime = (DateTime)value;
        if (dateTime.Kind == DateTimeKind.Utc || dateTime.Kind == DateTimeKind.Unspecified)
            dateTime = DateTime.SpecifyKind(dateTime, DateTimeKind.Utc).ToLocalTime();
        var pre = dateTime.ToString("yyyy年MM月dd日");
        var today = DateTime.Now.Date;
        if (dateTime.Date == today)
            pre = "今天";
        else if (dateTime.Date == today.AddDays(-1)) pre = "昨天";

        return $"{pre} {dateTime.ToString("HH点")}";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

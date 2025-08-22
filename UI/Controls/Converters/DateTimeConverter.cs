using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace UI.Controls.Converters;

public class DateTimeConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (!(value is DateTime)) return "参数错误";
        var dateTime = (DateTime)value;
        var pre = dateTime.ToString("yyyy年MM月dd日");
        if (dateTime.Date == DateTime.Now.Date)
            pre = "今天";
        else if (dateTime.Date == DateTime.Now.Date.AddDays(-1).Date) pre = "昨天";

        return $"{pre} {dateTime.ToString("HH点")}";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
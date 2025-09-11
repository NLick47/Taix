using System;
using System.Globalization;
using Avalonia.Data.Converters;
using UI.Controls.Select;

namespace UI.Themes.Select;

public class DateFormatConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is DateTime dateTime)
        {
            var selectType = parameter is DateSelectType type 
                ? type 
                : (parameter is string typeStr && Enum.TryParse(typeStr, out DateSelectType parsedType)
                    ? parsedType
                    : DateSelectType.Date);
            return selectType switch
            {
                DateSelectType.Date => dateTime.Day.ToString(), 
                DateSelectType.Month => dateTime.Month.ToString(), 
                DateSelectType.Year => dateTime.Year.ToString(), 
                _ =>  dateTime.Day.ToString()
            };
        }
        
        return value;
    }

    
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
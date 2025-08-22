namespace Core.Librarys;

public class Time
{
    public static string ToHoursString(double seconds)
    {
        var hours = seconds / 60 / 60;

        if (hours > 0.1) return hours.ToString("f2");

        return "0";
    }

    public static string ToString(int seconds)
    {
        if (seconds < 60) return seconds + "s";

        var minutes = (double)seconds / 60;
        if (minutes < 60)
        {
            if (minutes != (int)minutes)
            {
                var pointNumber = minutes - (int)minutes;
                var seconds_ = (int)(pointNumber * 60);
                return (int)minutes + "m" + (seconds_ > 0 ? seconds_ + "s" : "");
            }

            return minutes + "m";
        }

        var hours = minutes / 60;
        if (hours != (int)hours)
        {
            var pointNumber = hours - (int)hours;
            var minutes_ = (int)(pointNumber * 60);
            return (int)hours + "h" + (minutes_ > 0 ? minutes_ + "m" : "");
        }

        return hours + "h";
    }

    public static DateTime[] GetThisWeekDate()
    {
        DateTime weekStartDate = DateTime.Now, weekEndDate = DateTime.Now;
        if (DateTime.Now.DayOfWeek == DayOfWeek.Monday)
        {
            weekStartDate = DateTime.Now.Date;
            weekEndDate = DateTime.Now.Date.AddDays(6);
        }
        else
        {
            var weekNum = (int)DateTime.Now.DayOfWeek;
            if (weekNum == 0) weekNum = 7;
            weekNum -= 1;
            weekStartDate = DateTime.Now.Date.AddDays(-weekNum);
            weekEndDate = weekStartDate.Date.AddDays(6);
        }

        return new[] { weekStartDate, weekEndDate };
    }

    public static DateTime[] GetLastWeekDate()
    {
        DateTime weekStartDate = DateTime.Now, weekEndDate = DateTime.Now;

        var weekNum = (int)DateTime.Now.DayOfWeek;
        if (weekNum == 0) weekNum = 7;


        weekStartDate = DateTime.Now.Date.AddDays(-6 - weekNum);
        weekEndDate = weekStartDate.AddDays(6);

        return new[] { weekStartDate, weekEndDate };
    }

    public static DateTime[] GetMonthDate(DateTime date)
    {
        var dateStart = new DateTime(date.Year, date.Month, 1);
        var dateEnd = new DateTime(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month));

        return new[] { dateStart, dateEnd };
    }

    public static DateTime[] GetYearDate(DateTime date)
    {
        var dateStart = new DateTime(date.Year, 1, 1, 0, 0, 0);
        var dateEnd = new DateTime(date.Year, 12, DateTime.DaysInMonth(date.Year, 12), 23, 59, 59);
        return new[] { dateStart, dateEnd };
    }
}
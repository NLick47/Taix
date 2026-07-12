using System;
using System.Collections.Generic;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls.Primitives;
using ReactiveUI;

namespace Taix.Client.Controls.Select;

public class DayModel
{
    public DateTime Day { get; set; }
    public bool IsOut => Day.Date > DateTime.Now.Date;
    public bool IsDisabled { get; set; }
    public bool IsSelected { get; set; }
}

public class DateSelect : TemplatedControl
{
    public static readonly DirectProperty<DateSelect, DateSelectType> SelectTypeProperty =
        AvaloniaProperty.RegisterDirect<DateSelect, DateSelectType>(
            nameof(SelectType),
            o => o.SelectType,
            (o, v) => o.SelectType = v);

    public static readonly DirectProperty<DateSelect, DateTime> DateProperty =
        AvaloniaProperty.RegisterDirect<DateSelect, DateTime>(
            nameof(Date),
            o => o.Date,
            (o, v) => o.Date = v);

    public static readonly DirectProperty<DateSelect, int> YearProperty =
        AvaloniaProperty.RegisterDirect<DateSelect, int>(
            nameof(Year),
            o => o.Year,
            (o, v) => o.Year = v);

    public static readonly DirectProperty<DateSelect, int> MonthProperty =
        AvaloniaProperty.RegisterDirect<DateSelect, int>(
            nameof(Month),
            o => o.Month,
            (o, v) => o.Month = v);

    public static readonly DirectProperty<DateSelect, string> DateStrProperty =
        AvaloniaProperty.RegisterDirect<DateSelect, string>(
            nameof(DateStr),
            o => o.DateStr,
            (o, v) => o.DateStr = v);

    public static readonly DirectProperty<DateSelect, List<DayModel>> DaysProperty =
        AvaloniaProperty.RegisterDirect<DateSelect, List<DayModel>>(
            nameof(Days),
            o => o.Days,
            (o, v) => o.Days = v);

    public static readonly DirectProperty<DateSelect, bool> IsOpenProperty =
        AvaloniaProperty.RegisterDirect<DateSelect, bool>(
            nameof(IsOpen),
            o => o.IsOpen,
            (o, v) => o.IsOpen = v);

    public static readonly DirectProperty<DateSelect, bool> CanGoNextProperty =
        AvaloniaProperty.RegisterDirect<DateSelect, bool>(
            nameof(CanGoNext),
            o => o.CanGoNext,
            (o, v) => o.CanGoNext = v);

    private DateTime _date = DateTime.Now;
    private string _dateStr = string.Empty;
    private List<DayModel> _days = new();
    private bool _isOpen;
    private bool _canGoNext;
    private int _month;
    private DateSelectType _selectType;
    private int _year;

    public DateSelect()
    {
        ShowSelectCommand = ReactiveCommand.Create(OnShowSelect);
        SetYearCommand = ReactiveCommand.Create<object>(OnSetYear);
        SetMonthCommand = ReactiveCommand.Create<object>(OnSetMonth);
        SelectDayCommand = ReactiveCommand.Create<DayModel>(OnSelectDay);
        DoneCommand = ReactiveCommand.Create<object>(OnDone);
        GoPreviousCommand = ReactiveCommand.Create(OnGoPrevious);
        GoNextCommand = ReactiveCommand.Create(OnGoNext, this.WhenAnyValue(x => x.CanGoNext));
        _year = _date.Year;
        _month = _date.Month;
        UpdateCanGoNext();

        // SelectType 变化时同步刷新日期文案：DateStr 依赖 SelectType（日/周/月/年格式不同）
        this.GetObservable(SelectTypeProperty).Subscribe(_ =>
        {
            UpdateDays();
            UpdateDateStr();
        });
    }



    protected override Type StyleKeyOverride => typeof(DateSelect);

    public DateSelectType SelectType
    {
        get => _selectType;
        set => SetAndRaise(SelectTypeProperty, ref _selectType, value);
    }

    public DateTime Date
    {
        get => _date;
        set
        {
            if (SetAndRaise(DateProperty, ref _date, value))
            {
                UpdateDateStr();
                UpdateDays();
                UpdateCanGoNext();
            }
        }
    }


    private void OnDone(object obj)
    {
        var day = string.IsNullOrEmpty(DateStr) ? DateStr : "1";
        Date = new DateTime(Year, Month,  int.Parse(day));
        UpdateDateStr();
        IsOpen = false;
    }

    public int Year
    {
        get => _year;
        set
        {
            if (SetAndRaise(YearProperty, ref _year, value))
            {
                UpdateDays();
            }
        }
    }

    public int Month
    {
        get => _month;
        set
        {
            if (SetAndRaise(MonthProperty, ref _month, Math.Clamp(value, 1, 12)))
            {
                UpdateDays();
            }
        }
    }

    public string DateStr
    {
        get => _dateStr;
        set => SetAndRaise(DateStrProperty, ref _dateStr, value);
    }

    public List<DayModel> Days
    {
        get => _days;
        set => SetAndRaise(DaysProperty, ref _days, value);
    }



    public bool IsOpen
    {
        get => _isOpen;
        set => SetAndRaise(IsOpenProperty, ref _isOpen, value);
    }

    public ICommand ShowSelectCommand { get; }
    public ICommand SetYearCommand { get; }
    public ICommand SetMonthCommand { get; }
    public ICommand SelectDayCommand { get; }
    public ICommand GoPreviousCommand { get; }
    public ICommand GoNextCommand { get; }

    public ICommand DoneCommand { get; set; }

    public bool CanGoNext
    {
        get => _canGoNext;
        set => SetAndRaise(CanGoNextProperty, ref _canGoNext, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        UpdateDays();
        UpdateDateStr();
    }

    private void OnShowSelect()
    {
        if (SelectType == DateSelectType.Week) return; // 周视图无下拉面板
        IsOpen = !IsOpen;
        if (IsOpen)
        {
            Year = Date.Year;
            Month = Date.Month;
            UpdateDays();
        }
    }

    private void OnSetYear(object? delta)
    {
        if (int.TryParse(delta?.ToString(), out int year))
        {
            var newYear = Year + year;
            if (newYear >= 2020 && newYear <= DateTime.Now.Year)
            {
                Year = newYear;
            }
        }
    }

    private void OnSetMonth(object delta)
    {
        if (int.TryParse(delta?.ToString(), out int month))
        {
            var newMonth = Month + month;
            if (newMonth is >= 1 and <= 12)
            {
                Month = newMonth;
            }
        }
    }

    private void OnSelectDay(DayModel? dayModel)
    {
        if (dayModel == null || dayModel.IsDisabled) return;

        Date = dayModel.Day;
        IsOpen = false;
    }

    private void UpdateCanGoNext()
    {
        var now = DateTime.Now;
        CanGoNext = SelectType switch
        {
            DateSelectType.Week => GetWeekStart(Date) < GetWeekStart(now),
            DateSelectType.Month => new DateTime(Date.Year, Date.Month, 1) < new DateTime(now.Year, now.Month, 1),
            DateSelectType.Year => Date.Year < now.Year,
            _ => Date.Date < now.Date
        };
    }

    private static DateTime GetWeekStart(DateTime date)
    {
        var dayOfWeek = (int)date.DayOfWeek;
        dayOfWeek = dayOfWeek == 0 ? 7 : dayOfWeek;
        return date.Date.AddDays(-(dayOfWeek - 1));
    }

    private void OnGoPrevious()
    {
        Date = SelectType switch
        {
            DateSelectType.Week => Date.AddDays(-7),
            DateSelectType.Month => Date.AddMonths(-1),
            DateSelectType.Year => Date.AddYears(-1),
            _ => Date.AddDays(-1)
        };
    }

    private void OnGoNext()
    {
        if (!CanGoNext) return;
        var next = SelectType switch
        {
            DateSelectType.Week => Date.AddDays(7),
            DateSelectType.Month => Date.AddMonths(1),
            DateSelectType.Year => Date.AddYears(1),
            _ => Date.AddDays(1)
        };
        Date = next;
    }

    private void UpdateDays()
    {
        switch (SelectType)
        {
            case DateSelectType.Date:
                UpdateDateDays();
                break;
            case DateSelectType.Week:
                // 周视图无下拉面板
                break;
            case DateSelectType.Month:
                UpdateMonthDays();
                break;
            case DateSelectType.Year:
                UpdateYearDays();
                break;
        }
    }



    private void UpdateDateDays()
    {
        var list = new List<DayModel>();

        var startDay = new DateTime(Year, Month, 1);
        var startWeekNum = (int)startDay.DayOfWeek;
        startWeekNum = startWeekNum == 0 ? 7 : startWeekNum;
        startWeekNum -= 1;

        var days = DateTime.DaysInMonth(Year, Month);

        var preAppendDays = new List<DayModel>();
        for (var i = startWeekNum; i > 0; i--)
        {
            var date = startDay.AddDays(-i);
            preAppendDays.Add(new DayModel
            {
                Day = date,
                IsDisabled = true
            });
        }

        list.AddRange(preAppendDays);

        var now = DateTime.Now.Date;
        var selectedDate = Date.Date;
        for (var i = 1; i < days + 1; i++)
        {
            var currentDate = new DateTime(Year, Month, i);
            list.Add(new DayModel
            {
                Day = currentDate,
                IsSelected = currentDate.Date == selectedDate
            });
        }

        Days = list;
    }



    private void UpdateDateStr()
    {
        var now = DateTime.Now;
        var culture = SystemLanguage.CurrentCultureInfo;

        if (SelectType == DateSelectType.Date)
        {
            if (Date.Date == now.Date)
            {
                DateStr = Application.Current?.Resources["Today"] as string;
                return;
            }

            var dayOfWeek = culture.DateTimeFormat.GetShortestDayName(Date.DayOfWeek);
            try
            {
                var fmt = Date.Year == now.Year ? "M" : "Y";
                var baseStr = Date.ToString(fmt, culture);
                DateStr = $"{baseStr} {dayOfWeek}";
            }
            catch
            {
                DateStr = $"{Date.Year}/{Date.Month}/{Date.Day} {dayOfWeek}";
            }
            return;
        }

        if (SelectType == DateSelectType.Week)
        {
            var weekStart = GetWeekStart(Date);
            var weekEnd = weekStart.AddDays(6);
            if (weekStart <= now.Date && now.Date <= weekEnd)
            {
                DateStr = Application.Current?.Resources["ThisWeek"] as string ?? "This Week";
                return;
            }
            try
            {
                if (weekStart.Year == now.Year)
                    DateStr = $"{weekStart.Month}/{weekStart.Day}-{weekEnd.Month}/{weekEnd.Day}";
                else
                    DateStr = $"{weekStart.Year}/{weekStart.Month}/{weekStart.Day}-{weekEnd.Year}/{weekEnd.Month}/{weekEnd.Day}";
            }
            catch
            {
                DateStr = $"{weekStart:M}-{weekEnd:M}";
            }
            return;
        }

        if (SelectType == DateSelectType.Month)
        {
            try
            {
                var fmt = Date.Year == now.Year ? "MMMM" : "Y";
                DateStr = Date.ToString(fmt, culture);
            }
            catch
            {
                DateStr = $"{Date.Year}/{Date.Month}";
            }
            return;
        }

        DateStr = Date.ToString("yyyy", culture);
    }


    private void UpdateMonthDays()
    {
        var list = new List<DayModel>();
        var currentYear = Year;
        var now = DateTime.Now;

        for (int month = 1; month <= 12; month++)
        {
            var monthDate = new DateTime(currentYear, month, 1);
            var isSelectedMonth = month == Date.Month && currentYear == Date.Year;
            bool isDisabled = false;

            if (currentYear == now.Year)
            {
                isDisabled = month > now.Month;
            }
            else if (currentYear > now.Year)
            {
                isDisabled = true;
            }

            list.Add(new DayModel
            {
                Day = monthDate,
                IsSelected = isSelectedMonth,
                IsDisabled = isDisabled
            });
        }

        Days = list;
    }

    private void UpdateYearDays()
    {
        var list = new List<DayModel>();
        var startYear = 2020; // 最近20年
        var endYear = DateTime.Now.Year;

        for (int year = startYear; year <= endYear; year++)
        {
            var yearDate = new DateTime(year, 1, 1);
            var isSelectedYear = year == Date.Year;

            list.Add(new DayModel
            {
                Day = yearDate,
                IsSelected = isSelectedYear
            });
        }

        Days = list;
    }
}

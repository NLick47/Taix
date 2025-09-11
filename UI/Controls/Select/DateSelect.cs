using System;
using System.Collections.Generic;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls.Primitives;
using ReactiveUI;
using SharedLibrary;

namespace UI.Controls.Select;

public class DayModel
{
    public DateTime Day { get; set; }
   
    public bool IsOut => Day.Date > DateTime.Now.Date;
    
    public bool IsToday => Day.Date == DateTime.Now.Date;
    
    
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

    private DateTime _date = DateTime.Now;
    private string _dateStr = string.Empty;
    private List<DayModel> _days = new();
    private bool _isOpen;
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
        _year = _date.Year;
        _month = _date.Month;
        
        this.GetObservable(SelectTypeProperty).Subscribe(_ => UpdateDays());
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
    
    public ICommand DoneCommand { get; set; }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        UpdateDays();
        UpdateDateStr();
    }

    private void OnShowSelect()
    {
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
    
    private void UpdateDays()
    {
        switch (SelectType)
        {
            case DateSelectType.Date:
                UpdateDateDays();
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
        
        for (var i = 1; i < days + 1; i++) list.Add(new DayModel
        {
            Day = new DateTime(Year, Month, i)
        });

        Days = list;
    }



    private void UpdateDateStr()
    {
        if (SelectType == DateSelectType.Date && 
            Date.Day == DateTime.Now.Day)
        {
            DateStr = (Application.Current!.Resources["Today"] as string)!;
            return;
        }

        try
        {
            var culture = SystemLanguage.CurrentCultureInfo;
            DateStr = SelectType switch
            {
                DateSelectType.Month => Date.ToString("Y", culture),
                DateSelectType.Year => Date.ToString("yyyy", culture),
                _ => Date.ToString("d", culture)
            };
        }
        catch
        {
            DateStr = SelectType switch
            {
                DateSelectType.Month => Date.ToString("MMMM yyyy"),
                DateSelectType.Year => Date.ToString("yyyy"),
                _ => Date.ToString("d")
            };
        }
    }
    
    
    private void UpdateMonthDays()
    {
        var list = new List<DayModel>();
        var currentYear = Year;
        
        for (int month = 1; month <= 12; month++)
        {
            var monthDate = new DateTime(currentYear, month, 1);
            var isSelectedMonth = month == Date.Month && currentYear == Date.Year;
            
            list.Add(new DayModel
            {
                Day = monthDate,
                IsSelected = isSelectedMonth,
                IsDisabled = month > DateTime.Now.Month 
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
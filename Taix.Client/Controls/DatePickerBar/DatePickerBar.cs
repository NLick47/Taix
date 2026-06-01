using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Taix.Client.Controls.DatePickerBar;

public class DatePickerBar : TemplatedControl
{
    public static readonly DirectProperty<DatePickerBar, DatePickerShowType> ShowTypeProperty =
        AvaloniaProperty.RegisterDirect<DatePickerBar, DatePickerShowType>(
            nameof(ShowType),
            o => o.ShowType,
            (o, v) => o.ShowType = v);

    public static readonly DirectProperty<DatePickerBar, DateTime> SelectedDateProperty =
        AvaloniaProperty.RegisterDirect<DatePickerBar, DateTime>(
            nameof(SelectedDate),
            o => o.SelectedDate,
            (o, v) => o.SelectedDate = v);

    public static readonly DirectProperty<DatePickerBar, string> SelectedDateStringProperty =
        AvaloniaProperty.RegisterDirect<DatePickerBar, string>(
            nameof(SelectedDateString),
            o => o.SelectedDateString,
            (o, v) => o.SelectedDateString = v);

    public static readonly StyledProperty<bool> IsShowDatePickerPopupProperty =
        AvaloniaProperty.Register<DatePickerBar, bool>(nameof(IsShowDatePickerPopup));

    private readonly List<DateTime> DateList;
    private readonly Dictionary<DateTime, DatePickerBarItem> ItemsDictionary;

    private DateTime _selectedDate;

    private string _selectedDateString = string.Empty;
    private DatePickerShowType _showType = DatePickerShowType.Day;


    private StackPanel Container;

    private int dataCount;

    private Border Date;

    //  日期选择弹出层
    private Popup DatePickerPopup;
    private StackPanel MonthSelect;
    private int renderIndex;
    private ScrollViewer ScrollViewer;
    private UniformGrid YearsGrid, MonthsGrid;

    // 网格项集合
    private readonly List<DatePickerGridItem> YearItems = new();
    private readonly List<DatePickerGridItem> MonthItems = new();
    private int currentYear;
    private int currentMonth;

    public DatePickerBar()
    {
        ItemsDictionary = new Dictionary<DateTime, DatePickerBarItem>();
        DateList = new List<DateTime>();
    }

    public DatePickerShowType ShowType
    {
        get => _showType;
        set => SetAndRaise(ShowTypeProperty, ref _showType, value);
    }

    public DateTime SelectedDate
    {
        get => _selectedDate;
        set => SetAndRaise(SelectedDateProperty, ref _selectedDate, value);
    }

    public string SelectedDateString
    {
        get => _selectedDateString;
        set => SetAndRaise(SelectedDateStringProperty, ref _selectedDateString, value);
    }

    public bool IsShowDatePickerPopup
    {
        get => GetValue(IsShowDatePickerPopupProperty);
        set => SetValue(IsShowDatePickerPopupProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(DatePickerBar);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var control = change.Sender as DatePickerBar;
        if (change.Property == ShowTypeProperty && change.NewValue != change.OldValue)
            control?.Render(control.SelectedDate);
        if (change.Property == SelectedDateProperty && change.NewValue != change.OldValue)
        {
            DateTime.TryParse(change.NewValue.ToString(), out var newDateTime);
            control?.ScrollToActive(newDateTime);
            control?.UpdateDateString();
            control?.UpdateGridSelection();
            control?.SyncCurrentDateFromSelected();
        }
    }

    private void SyncCurrentDateFromSelected()
    {
        if (SelectedDate != DateTime.MinValue)
        {
            currentYear = SelectedDate.Year;
            currentMonth = SelectedDate.Month;
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Container = e.NameScope.Get<StackPanel>("Container");
        ScrollViewer = e.NameScope.Get<ScrollViewer>("ScrollViewer");
        DatePickerPopup = e.NameScope.Get<Popup>("DatePickerPopup");
        Date = e.NameScope.Get<Border>("Date");
        YearsGrid = e.NameScope.Get<UniformGrid>("YearsGrid");
        MonthsGrid = e.NameScope.Get<UniformGrid>("MonthsGrid");
        MonthSelect = e.NameScope.Get<StackPanel>("MonthSelect");

        Init();

        // 根据SelectedDate初始化currentYear和currentMonth
        if (SelectedDate != DateTime.MinValue)
        {
            currentYear = SelectedDate.Year;
            currentMonth = SelectedDate.Month;
        }

        //  渲染日期
        Render(SelectedDate != DateTime.MinValue ? SelectedDate : DateTime.Now);
    }

    private void Init()
    {
        Date.PointerPressed += OnDatePointerPressed;

        // 根据SelectedDate初始化currentYear和currentMonth
        if (SelectedDate != DateTime.MinValue)
        {
            currentYear = SelectedDate.Year;
            currentMonth = SelectedDate.Month;
        }
        else
        {
            currentYear = DateTime.Now.Year;
            currentMonth = DateTime.Now.Month;
        }

        // 初始化年份网格
        var startYear = 2021;
        var endYear = DateTime.Now.Year;

        for (var year = startYear; year <= endYear; year++)
        {
            var item = new DatePickerGridItem { Title = year.ToString(), Value = year };
            item.PointerPressed += OnYearSelectedHandler;
            YearsGrid.Children.Add(item);
            YearItems.Add(item);
            if (year == currentYear) item.IsSelected = true;
        }

        if (ShowType == DatePickerShowType.Day)
        {
            MonthSelect.IsVisible = true;

            // 初始化月份网格
            for (var month = 1; month <= 12; month++)
            {
                var item = new DatePickerGridItem { Title = month.ToString(), Value = month };
                item.PointerPressed += OnMonthSelectedHandler;
                MonthsGrid.Children.Add(item);
                MonthItems.Add(item);
                if (month == currentMonth) item.IsSelected = true;
            }
        }
        else
        {
            // ShowType=Month 时，月份选择通过底部月份条完成，不需要弹窗中的月份网格
            MonthSelect.IsVisible = false;
        }
    }

    private void OnYearSelected(int year)
    {
        currentYear = year;
        UpdateGridSelection();

        // 根据ShowType更新SelectedDate
        if (ShowType == DatePickerShowType.Day)
        {
            SelectedDate = new DateTime(year, currentMonth, 1);
            Render(SelectedDate);
        }
        else if (ShowType == DatePickerShowType.Month)
        {
            // ShowType=Month时，底部显示月份条，需要重新渲染
            // 保持当前月份，但切换到新年份
            SelectedDate = new DateTime(year, currentMonth, 1);
            Render(SelectedDate);
            // Month模式下选完年份直接关闭
            IsShowDatePickerPopup = false;
        }
    }

    private void OnMonthSelected(int month)
    {
        currentMonth = month;
        UpdateGridSelection();
        SelectedDate = new DateTime(currentYear, month, 1);
        Render(SelectedDate);
        IsShowDatePickerPopup = false;
    }

    private void UpdateGridSelection()
    {
        // 更新年份选中状态
        foreach (var item in YearItems)
        {
            item.IsSelected = item.Value == currentYear;
        }

        // 更新月份选中状态
        foreach (var item in MonthItems)
        {
            item.IsSelected = item.Value == currentMonth;
        }
    }

    private void OnDatePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        IsShowDatePickerPopup = !IsShowDatePickerPopup;
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        Date.PointerPressed -= OnDatePointerPressed;

        // 清理年份网格项的事件订阅
        foreach (var item in YearItems)
        {
            item.PointerPressed -= OnYearSelectedHandler;
        }
        YearItems.Clear();

        // 清理月份网格项的事件订阅
        foreach (var item in MonthItems)
        {
            item.PointerPressed -= OnMonthSelectedHandler;
        }
        MonthItems.Clear();
    }


    private void OnYearSelectedHandler(object? sender, PointerPressedEventArgs e)
    {
        if (sender is DatePickerGridItem item)
        {
            OnYearSelected(item.Value);
        }
    }

    private void OnMonthSelectedHandler(object? sender, PointerPressedEventArgs e)
    {
        if (sender is DatePickerGridItem item)
        {
            OnMonthSelected(item.Value);
        }
    }

    private void UpdateDateString()
    {
        if (ShowType == DatePickerShowType.Day)
            SelectedDateString = SelectedDate.ToString("yyyy/MM");
        else
            SelectedDateString = SelectedDate.ToString("yyyy");
    }

    private void AddItem(DateTime date)
    {
        if (Container != null)
        {
            if (DateList.IndexOf(date) != -1) return;
            renderIndex++;
            var control = new DatePickerBarItem();
            control.Title = date.Day.ToString();
            control.Date = date;
            control.PointerPressed += (e, c) =>
            {
                if (date > DateTime.Now.Date) return;
                ScrollToActive(date);
            };

            if (renderIndex == dataCount)
                control.Loaded += (e, c) =>
                {
                    if (SelectedDate == DateTime.MinValue)
                    {
                        if (date == DateTime.Now.Date) SelectedDate = DateTime.Now.Date;
                    }
                    else
                    {
                        ScrollToActive(SelectedDate);
                    }
                };
            var next = DateList.IndexOf(date.AddDays(+1));

            if (next != -1)
            {
                var index = next - 1;
                if (index < 0) index = 0;
                DateList.Insert(index, date);
                ItemsDictionary.Add(date, control);
                Container.Children.Insert(index, control);
            }
            else
            {
                DateList.Add(date);
                ItemsDictionary.Add(date, control);
                Container.Children.Add(control);
            }
        }
    }

    private void Render(DateTime date)
    {
        if (Container == null) return;
        dataCount = 0;
        renderIndex = 0;

        DateList.Clear();
        ItemsDictionary.Clear();
        Container.Children.Clear();

        if (ShowType == DatePickerShowType.Day)
        {
            dataCount = DateTime.DaysInMonth(date.Year, date.Month);
            for (var i = 1; i <= dataCount; i++) AddItem(new DateTime(date.Year, date.Month, i));
        }

        if (ShowType == DatePickerShowType.Month)
        {
            dataCount = 12;

            for (var i = 1; i < 13; i++)
            {
                var data = new DateTime(date.Year, i, 1);
                AddMonthItem(data);
            }
        }
    }

    private void AddMonthItem(DateTime date)
    {
        if (Container != null)
        {
            if (DateList.IndexOf(date) != -1) return;
            renderIndex++;

            var control = new DatePickerBarItem();
            control.Title = date.Month.ToString();
            control.Date = date;
            control.PointerPressed += (e, c) =>
            {
                if (date > DateTime.Now.Date) return;
                ScrollToActive(date);
            };

            if (renderIndex == dataCount)
                control.Loaded += (e, c) =>
                {
                    if (SelectedDate == DateTime.MinValue)
                    {
                        if (date.Year == DateTime.Now.Date.Year
                            && date.Month == DateTime.Now.Date.Month)
                            SelectedDate = date;
                    }
                    else
                    {
                        ScrollToActive(SelectedDate);
                    }
                };


            DateList.Add(date);
            ItemsDictionary.Add(date, control);
            Container.Children.Add(control);
        }
    }

    private void ScrollToActive(DateTime date)
    {
        if (ItemsDictionary.Count == 0) return;
        if (ShowType == DatePickerShowType.Month)
            date = new DateTime(date.Year, date.Month, 1);
        else
            date = new DateTime(date.Year, date.Month, date.Day);

        if (!ItemsDictionary.ContainsKey(date)) return;

        // 取消所有选中状态（SelectedDate可能带时间，字典key是归零的，无法直接匹配）
        foreach (var item in ItemsDictionary.Values)
            item.IsSelected = false;

        // 更新视觉选中状态和滚动位置
        ItemsDictionary[date].IsSelected = true;

        var control = ItemsDictionary[date];

        var transform = control.TransformToVisual(ScrollViewer);
        if (!transform.HasValue) return;
        var relativePoint = transform.Value.Transform(new Point(0, 0));
        var scrollTo = relativePoint.X - ScrollViewer.Bounds.Width / 2 + control.Bounds.Width / 2;
        if (scrollTo < 0) scrollTo = 0;
        ScrollViewer.Offset = new Vector(scrollTo, ScrollViewer.Offset.Y);
    }
}

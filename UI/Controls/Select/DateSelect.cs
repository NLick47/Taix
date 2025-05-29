using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using ReactiveUI;
using SharedLibrary;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reactive;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Collections;

namespace UI.Controls.Select
{
    public class DayModel
    {
        public DateTime Day { get; set; }

        public string DayText
        {
            get { return Day.Day.ToString(); }
        }

        public bool IsToday
        {
            get { return Day.Date == DateTime.Now.Date; }
        }

        public bool IsOut
        {
            get { return Day.Date > DateTime.Now.Date; }
        }

        public bool IsDisabled { get; set; }
    }

    public class DateSelect : TemplatedControl
    {
        protected override Type StyleKeyOverride => typeof(DateSelect);

        private DateSelectType _selectType;

        public static readonly DirectProperty<DateSelect, DateSelectType> SelectTypeProperty =
            AvaloniaProperty.RegisterDirect<DateSelect, DateSelectType>(
                nameof(SelectType),
                o => o.SelectType,
                (o, v) => o.SelectType = v);

        public DateSelectType SelectType
        {
            get => _selectType;
            set => SetAndRaise(SelectTypeProperty, ref _selectType, value);
        }

        private DateTime _date;

        public static readonly DirectProperty<DateSelect, DateTime> DateProperty =
            AvaloniaProperty.RegisterDirect<DateSelect, DateTime>(
                nameof(Date),
                o => o.Date,
                (o, v) => o.Date = v);

        public DateTime Date
        {
            get => _date;
            set => SetAndRaise(DateProperty, ref _date, value);
        }

        private int _year;

        public static readonly DirectProperty<DateSelect, int> YearProperty =
            AvaloniaProperty.RegisterDirect<DateSelect, int>(
                nameof(Year),
                o => o.Year,
                (o, v) => o.Year = v);

        public int Year
        {
            get => _year;
            set => SetAndRaise(YearProperty, ref _year, value);
        }

        private int _month;

        public static readonly DirectProperty<DateSelect, int> MonthProperty =
            AvaloniaProperty.RegisterDirect<DateSelect, int>(
                nameof(Month),
                o => o.Month,
                (o, v) => o.Month = v);

        public int Month
        {
            get => _month;
            set => SetAndRaise(MonthProperty, ref _month, value);
        }

        private DayModel _day;

        public static readonly DirectProperty<DateSelect, DayModel> DayProperty =
            AvaloniaProperty.RegisterDirect<DateSelect, DayModel>(
                nameof(Day),
                o => o.Day,
                (o, v) => o.Day = v);

        public DayModel Day
        {
            get => _day;
            set => SetAndRaise(DayProperty, ref _day, value);
        }

        private string _dateStr = string.Empty;

        public static readonly DirectProperty<DateSelect, string> DateStrProperty =
            AvaloniaProperty.RegisterDirect<DateSelect, string>(
                nameof(DateStr),
                o => o.DateStr,
                (o, v) => o.DateStr = v);

        public string DateStr
        {
            get => _dateStr;
            set => SetAndRaise(DateStrProperty, ref _dateStr, value);
        }

        private List<DayModel> _days = new ();

        public static readonly DirectProperty<DateSelect, List<DayModel>> DaysProperty =
            AvaloniaProperty.RegisterDirect<DateSelect, List<DayModel>>(
                nameof(Days),
                o => o.Days,
                (o, v) => o.Days = v);

        public List<DayModel> Days
        {
            get => _days;
            set => SetAndRaise(DaysProperty, ref _days, value);
        }

        private bool _isOpen;

        public static readonly DirectProperty<DateSelect, bool> IsOpenProperty =
            AvaloniaProperty.RegisterDirect<DateSelect, bool>(
                nameof(IsOpen),
                o => o.IsOpen,
                (o, v) => o.IsOpen = v);

        public bool IsOpen
        {
            get => _isOpen;
            set => SetAndRaise(IsOpenProperty, ref _isOpen, value);
        }

        public ICommand ShowSelectCommand { get; }
        public ICommand SetYearCommand { get; }
        public ICommand SetMonthCommand { get; }
        public ReactiveCommand<object, Unit> DoneCommand { get; }

        private bool IsFirstClick = false;
        private Border SelectContainer;
        private DateTime SelectedDay;

        public DateSelect()
        {
            ShowSelectCommand = ReactiveCommand.Create<object>(OnShowSelect);
            SetYearCommand = ReactiveCommand.Create<object>(OnSetYear);
            SetMonthCommand = ReactiveCommand.Create<object>(OnSetMonth);
            DoneCommand = ReactiveCommand.Create<object>(OnDone);
            Year = Date.Year;
            Month = Date.Month;
            SelectedDay = Date.Date;
        }


        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == DateProperty)
            {
                OnDateChanged(change);
            }
        }

        private static void OnDateChanged(AvaloniaPropertyChangedEventArgs e)
        {
            var control = e.Sender as DateSelect;
            control.Year = control.Date.Year;
            control.Month = control.Date.Month;
            control.SelectedDay = control.Date.Date;
        }


        private void OnDone(object obj)
        {
            Date = new DateTime(Year, Month, Day != null ? int.Parse(Day.DayText) : 1);
            if (Day != null)
            {
                SelectedDay = Day.Day;
            }

            UpdateDateStr();

            IsOpen = false;
        }


        private void OnSetMonth(object obj)
        {
            int newMonth = Month + int.Parse(obj.ToString());
            if (newMonth < 1 || newMonth > 12)
            {
                return;
            }

            Month = newMonth;

            UpdateDays();
        }


        private void OnSetYear(object obj)
        {
            int newYear = Year + int.Parse(obj.ToString());
            if (newYear < 2020 || newYear > DateTime.Now.Year)
            {
                return;
            }

            Year = newYear;
            UpdateDays();
        }

        private void OnShowSelect(object obj)
        {
            IsOpen = true;

            IsFirstClick = true;
        }

        private void SelectContainer_Loaded(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("加载");
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            SelectContainer = e.NameScope.Get<Border>("SelectContainer");

            UpdateDays();

            UpdateDateStr();
        }

        private void UpdateDays()
        {
            var list = new List<DayModel>();

            var startDay = new DateTime(Year, Month, 1);
            var startWeekNum = (int)startDay.DayOfWeek;
            startWeekNum = startWeekNum == 0 ? 7 : startWeekNum;
            startWeekNum -= 1;

            //  该月天数
            int days = DateTime.DaysInMonth(Year, Month);
            //  该月最后一天的日期
            var lastDay = new DateTime(Year, Month, days);

            var preAppendDays = new List<DayModel>();
            //  需要向前补日期
            for (int i = startWeekNum; i > 0; i--)
            {
                var date = startDay.AddDays(-i);
                preAppendDays.Add(new DayModel()
                {
                    Day = date.Date,
                    IsDisabled = true,
                });
            }

            list.AddRange(preAppendDays);

            //  当月日期
            for (int i = 1; i < days + 1; i++)
            {
                list.Add(new DayModel() { Day = new DateTime(Year, Month, i).Date });
            }

            Days = list;
            Day = list.Where(m => m.Day == SelectedDay).FirstOrDefault();
        }

        private void UpdateDateStr()
        {
            var culture = SystemLanguage.CurrentCultureInfo;
            DateStr = Date.ToString("d", culture);
            if (Date.Date == DateTime.Now.Date)
            {
                DateStr = Application.Current.Resources["Today"] as string;
            }

            if (SelectType == DateSelectType.Month)
            {
                DateStr = Date.ToString("Y", culture);
            }
            else if (SelectType == DateSelectType.Year)
            {
                DateStr = Date.ToString("yyyy", culture);
            }
        }
    }
}
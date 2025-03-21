﻿using Avalonia;
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

namespace UI.Controls.Select
{
    public class DayModel
    {
        public DateTime Day { get; set; }
        public string DayText
        {
            get
            {
                return Day.Day.ToString();
            }
        }
        public bool IsToday
        {
            get
            {
                return Day.Date == DateTime.Now.Date;
            }
        }
        public bool IsOut
        {
            get
            {
                return Day.Date > DateTime.Now.Date;
            }
        }
        public bool IsDisabled { get; set; }
    }
    public class DateSelect : TemplatedControl
    {

        protected override Type StyleKeyOverride => typeof(DateSelect);

        public DateSelectType SelectType
        {
            get { return GetValue(SelectTypeProperty); }
            set { SetValue(SelectTypeProperty, value); }
        }
        public static readonly StyledProperty<DateSelectType> SelectTypeProperty =
            AvaloniaProperty.Register<DateSelect, DateSelectType>(nameof(SelectType));
        public DateTime Date
        {
            get { return GetValue(DateProperty); }
            set { SetValue(DateProperty, value); }
        }
        public static readonly StyledProperty<DateTime> DateProperty =
            AvaloniaProperty.Register<DateSelect, DateTime>(nameof(Date));


        public int Year
        {
            get { return GetValue(YearProperty); }
            set { SetValue(YearProperty, value); }
        }
        public static readonly StyledProperty<int> YearProperty =
            AvaloniaProperty.Register<DateSelect, int>(nameof(Year));

        public int Month
        {
            get { return GetValue(MonthProperty); }
            set { SetValue(MonthProperty, value); }
        }

        public static readonly StyledProperty<int> MonthProperty =
            AvaloniaProperty.Register<DateSelect, int>(nameof(Month));

        public DayModel Day
        {
            get { return GetValue(DayProperty); }
            set { SetValue(DayProperty, value); }
        }
        public static readonly StyledProperty<DayModel> DayProperty =
            AvaloniaProperty.Register<DateSelect, DayModel>(nameof(Day));
        public string DateStr
        {
            get { return GetValue(DateStrProperty); }
            set { SetValue(DateStrProperty, value); }
        }
        public static readonly StyledProperty<string> DateStrProperty =
            AvaloniaProperty.Register<DateSelect, string>(nameof(DateSelect));
        public List<DayModel> Days
        {
            get { return GetValue(DaysProperty); }
            set { SetValue(DaysProperty, value); }
        }
        public static readonly StyledProperty<List<DayModel>> DaysProperty =
            AvaloniaProperty.Register<DateSelect, List<DayModel>>(nameof(Days));

        public bool IsOpen
        {
            get { return GetValue(IsOpenProperty); }
            set { SetValue(IsOpenProperty, value); }
        }
        public static readonly StyledProperty<bool> IsOpenProperty =
            AvaloniaProperty.Register<DateSelect, bool>(nameof(IsOpen));

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
            DateStr = Date.ToString("d",culture);
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
                DateStr = Date.ToString("yyyy",culture);

            }

            
        }
    }
}

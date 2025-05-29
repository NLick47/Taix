using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using DynamicData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UI.Controls.List;

namespace UI.Controls.DatePickerBar
{
    public class DatePickerBar : TemplatedControl
    {
        private DatePickerShowType _showType = DatePickerShowType.Day;
        public static readonly DirectProperty<DatePickerBar, DatePickerShowType> ShowTypeProperty =
            AvaloniaProperty.RegisterDirect<DatePickerBar, DatePickerShowType>(
                nameof(ShowType),
                o => o.ShowType,
                (o, v) => o.ShowType = v);
        public DatePickerShowType ShowType
        {
            get => _showType;
            set => SetAndRaise(ShowTypeProperty, ref _showType, value);
        }

        private DateTime _selectedDate;
        public static readonly DirectProperty<DatePickerBar, DateTime> SelectedDateProperty =
            AvaloniaProperty.RegisterDirect<DatePickerBar, DateTime>(
                nameof(SelectedDate),
                o => o.SelectedDate,
                (o, v) => o.SelectedDate = v);
        public DateTime SelectedDate
        {
            get => _selectedDate;
            set => SetAndRaise(SelectedDateProperty, ref _selectedDate, value);
        }

        private string _selectedDateString = string.Empty;
        public static readonly DirectProperty<DatePickerBar, string> SelectedDateStringProperty =
            AvaloniaProperty.RegisterDirect<DatePickerBar, string>(
                nameof(SelectedDateString),
                o => o.SelectedDateString,
                (o, v) => o.SelectedDateString = v);
        public string SelectedDateString
        {
            get => _selectedDateString;
            set => SetAndRaise(SelectedDateStringProperty, ref _selectedDateString, value);
        }

        private bool _isShowDatePickerPopup;
        public static readonly DirectProperty<DatePickerBar, bool> IsShowDatePickerPopupProperty =
            AvaloniaProperty.RegisterDirect<DatePickerBar, bool>(
                nameof(IsShowDatePickerPopup),
                o => o.IsShowDatePickerPopup,
                (o, v) => o.IsShowDatePickerPopup = v);
        public bool IsShowDatePickerPopup
        {
            get => _isShowDatePickerPopup;
            set => SetAndRaise(IsShowDatePickerPopupProperty, ref _isShowDatePickerPopup, value);
        }
        
        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            var control = change.Sender as DatePickerBar;
            if (change.Property == ShowTypeProperty && change.NewValue != change.OldValue)
            {
                control?.Render(control.SelectedDate);
            }
            if (change.Property == SelectedDateProperty && change.NewValue != change.OldValue)
            {
                DateTime.TryParse(change.NewValue.ToString(), out DateTime newDateTime);
                control?.ScrollToActive(newDateTime);
                control?.UpdateDateString();
            }
        }

      

        private StackPanel Container;
        private ScrollViewer ScrollViewer;
        private Dictionary<DateTime, DatePickerBarItem> ItemsDictionary;
        private List<DateTime> DateList;

        private int dataCount = 0;
        private int renderIndex = 0;
        //  日期选择弹出层
        private Popup DatePickerPopup;
        private BaseList YearsList, MonthsList;
        private Border Date;
        private StackPanel MonthSelect;

        public DatePickerBar()
        {
            ItemsDictionary = new Dictionary<DateTime, DatePickerBarItem>();
            DateList = new List<DateTime>();
        }

        protected override Type StyleKeyOverride => typeof(DatePickerBar);

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            Container = e.NameScope.Get<StackPanel>("Container");
            ScrollViewer = e.NameScope.Get<ScrollViewer>("ScrollViewer");
            DatePickerPopup = e.NameScope.Get<Popup>("DatePickerPopup");
            Date = e.NameScope.Get<Border>("Date");
            YearsList = e.NameScope.Get<BaseList>("YearsList");
            MonthsList = e.NameScope.Get<BaseList>("MonthsList");
            MonthSelect = e.NameScope.Get<StackPanel>("MonthSelect");

            Init();

            //  渲染日期
            Render(DateTime.Now);
        }

        private void Init()
        {
            //  填充年份数据
            YearsList.SelectedItem = DateTime.Now.Year.ToString();
            //YearsList.SelectedItem = "2073";
            Date.PointerPressed += OnDatePointerPressed;
            for (int i = 2021; i <= DateTime.Now.Year; i++)
            {
                YearsList.Items.Add(i.ToString());
            }
            YearsList.SelectedItemChanged += DateChanged;

            if (ShowType == DatePickerShowType.Day)
            {
                //  填充月份数据
                MonthSelect.IsVisible = true;

                MonthsList.SelectedItem = DateTime.Now.Month.ToString();
                //MonthsList.SelectedItem = "1";

                for (int i = 1; i <= 12; i++)
                {
                    MonthsList.Items.Add(i.ToString());
                }

                MonthsList.SelectedItemChanged += DateChanged;

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
            MonthsList.SelectedItemChanged -= DateChanged;
            YearsList.SelectedItemChanged -= DateChanged;
        }

        private void DateChanged(object sender, EventArgs e)
        {
            if (ShowType == DatePickerShowType.Day)
            {
                SelectedDate = new DateTime(int.Parse(YearsList.SelectedItem), int.Parse(MonthsList.SelectedItem), 1);
            }
            else
            {
                SelectedDate = new DateTime(int.Parse(YearsList.SelectedItem), 1, 1);
            }
            Render(SelectedDate);
        }

        private void UpdateDateString()
        {
            if (ShowType == DatePickerShowType.Day)
            {
                SelectedDateString = SelectedDate.ToString("yyyy/MM");
            }
            else
            {
                SelectedDateString = SelectedDate.ToString("yyyy");
            }
        }

        private void AddItem(DateTime date)
        {
            if (Container != null)
            {
                if (DateList.IndexOf(date) != -1)
                {
                    return;
                }
                renderIndex++;
                var control = new DatePickerBarItem();
                control.Title = date.Day.ToString();
                control.Date = date;
                control.PointerPressed += (e, c) =>
                {
                    if (date > DateTime.Now.Date)
                    {
                        return;
                    }
                    ScrollToActive(date);
                };

                if (renderIndex == dataCount)
                {

                    control.Loaded += (e, c) =>
                    {

                        if (SelectedDate == DateTime.MinValue)
                        {
                            if (date == DateTime.Now.Date)
                            {
                                SelectedDate = DateTime.Now.Date;
                            }
                        }
                        else
                        {
                            ScrollToActive(SelectedDate);
                        }
                    };
                }
                //  后一天
                int next = DateList.IndexOf(date.AddDays(+1));

                if (next != -1)
                {
                    int index = next - 1;
                    if (index < 0)
                    {
                        index = 0;
                    }
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
            if (Container == null)
            {
                return;
            }
            dataCount = 0;
            renderIndex = 0;

            DateList.Clear();
            ItemsDictionary.Clear();
            Container.Children.Clear();

            if (ShowType == DatePickerShowType.Day)
            {
                dataCount = DateTime.DaysInMonth(date.Year, date.Month);
                for (int i = 1; i <= dataCount; i++)
                {
                    AddItem(new DateTime(date.Year, date.Month, i));
                }
            }

            if (ShowType == DatePickerShowType.Month)
            {
                dataCount = 12;

                for (int i = 1; i < 13; i++)
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
                if (DateList.IndexOf(date) != -1)
                {
                    return;
                }
                renderIndex++;

                var control = new DatePickerBarItem();
                control.Title = date.Month.ToString();
                control.Date = date;
                control.PointerPressed += (e, c) =>
                {
                    if (date > DateTime.Now.Date)
                    {
                        return;
                    }
                    ScrollToActive(date);
                };

                if (renderIndex == dataCount)
                {
                    control.Loaded += (e, c) =>
                    {
                        if (SelectedDate == DateTime.MinValue)
                        {
                            if (date.Year == DateTime.Now.Date.Year
                            && date.Month == DateTime.Now.Date.Month)
                            {
                                SelectedDate = date;
                            }
                        }
                        else
                        {
                            ScrollToActive(SelectedDate);
                        }
                    };
                }


                DateList.Add(date);
                ItemsDictionary.Add(date, control);
                Container.Children.Add(control);
            }
        }

        private void ScrollToActive(DateTime date)
        {

            if (ItemsDictionary.Count == 0)
            {
                return;
            }
            if (ShowType == DatePickerShowType.Month)
            {
                date = new DateTime(date.Year, date.Month, 1);
            }
            else
            {
                date = new DateTime(date.Year, date.Month, date.Day);
            }

            if (!ItemsDictionary.ContainsKey(date))
            {
                return;
            }
            if (ItemsDictionary.ContainsKey(SelectedDate))
            {
                //  如果存在旧的选中，先取消
                ItemsDictionary[SelectedDate].IsSelected = false;
            }

            if (date != SelectedDate)
            {
                SelectedDate = date;
            }


            ItemsDictionary[date].IsSelected = true;

            var control = ItemsDictionary[SelectedDate];

            var transform = control.TransformToVisual(ScrollViewer);
            if (!transform.HasValue) return;
            var relativePoint = transform.Value.Transform(new Point(0, 0));
            double scrollTo = relativePoint.X - (ScrollViewer.Bounds.Width / 2) + control.Bounds.Width / 2;
            if (scrollTo < 0)
            {
                scrollTo = 0;
            }
            ScrollViewer.Offset = new Vector(scrollTo, ScrollViewer.Offset.Y);
        }
    }
}

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
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
        public DatePickerShowType ShowType
        {
            get { return (DatePickerShowType)GetValue(ShowTypeProperty); }
            set { SetValue(ShowTypeProperty, value); }
        }

        public static readonly StyledProperty<DatePickerShowType> ShowTypeProperty =
        AvaloniaProperty.Register<DatePickerBar, DatePickerShowType>(nameof(ShowType), DatePickerShowType.Day);

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

        public DateTime SelectedDate
        {
            get { return (DateTime)GetValue(SelectedDateProperty); }
            set { SetValue(SelectedDateProperty, value); }
        }
        public static readonly StyledProperty<DateTime> SelectedDateProperty =
            AvaloniaProperty.Register<DatePickerBar,DateTime>(nameof(SelectedDate));

        public string SelectedDateString
        {
            get { return (string)GetValue(SelectedDateStringProperty); }
            set { SetValue(SelectedDateStringProperty, value); }
        }
        public static readonly StyledProperty<string> SelectedDateStringProperty =
            AvaloniaProperty.Register<DatePickerBar,string>(nameof(SelectedDateString));

        public bool IsShowDatePickerPopup
        {
            get { return (bool)GetValue(IsShowDatePickerPopupProperty); }
            set { SetValue(IsShowDatePickerPopupProperty, value); }
        }
        public static readonly StyledProperty<bool> IsShowDatePickerPopupProperty =
            AvaloniaProperty.Register<DatePickerBar,bool>(nameof(IsShowDatePickerPopup));

        private StackPanel Container;
        private ScrollViewer ScrollViewer;
        private Dictionary<DateTime, DatePickerBarItem> ItemsDictionary;
        private List<DateTime> DateList;

        private int dataCount = 0;
        private int renderIndex = 0;
        //  选中标记块
        private Border ActiveBlock;
        //  日期选择弹出层
        private Popup DatePickerPopup;
        private BaseList YearsList, MonthsList;
        private Border Date;
        private ScrollViewer YearsListScrollViewer;
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
            ActiveBlock = e.NameScope.Get<Border>("ActiveBlock");
            ScrollViewer = e.NameScope.Get<ScrollViewer>("ScrollViewer");
            DatePickerPopup = e.NameScope.Get<Popup>("DatePickerPopup");
            Date = e.NameScope.Get<Border>("Date");
            YearsList = e.NameScope.Get<BaseList>("YearsList");
            MonthsList = e.NameScope.Get<BaseList>("MonthsList");
            YearsListScrollViewer = e.NameScope.Get<ScrollViewer>("YearsListScrollViewer");
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

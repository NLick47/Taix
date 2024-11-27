using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Core.Librarys;
using Core.Servicers.Instances;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using UI.Controls.Base;
using UI.Controls.Charts.Model;
using UI.Extensions;

namespace UI.Controls.Charts
{
    public class Charts : TemplatedControl
    {
        public ChartsType ChartsType
        {
            get { return (ChartsType)GetValue(ChartsTypeProperty); }
            set { SetValue(ChartsTypeProperty, value); }
        }
        public static readonly StyledProperty<ChartsType> ChartsTypeProperty =
            AvaloniaProperty.Register<Charts, ChartsType>(nameof(ChartsType));

        public double MaxValueLimit
        {
            get { return (double)GetValue(MaxValueLimitProperty); }
            set { SetValue(MaxValueLimitProperty, value); }
        }
        public static readonly StyledProperty<double> MaxValueLimitProperty =
            AvaloniaProperty.Register<Charts, double>(nameof(MaxValueLimit), 0);

        public int ShowLimit
        {
            get { return (int)GetValue(ShowLimitProperty); }
            set { SetValue(ShowLimitProperty, value); }
        }
        public static readonly StyledProperty<int> ShowLimitProperty =
            AvaloniaProperty.Register<Charts, int>(nameof(ShowLimit), 0);

        public IEnumerable<ChartsDataModel> Data
        {
            get { return (IEnumerable<ChartsDataModel>)GetValue(DataProperty); }
            set { SetValue(DataProperty, value); }
        }

        public static readonly StyledProperty<IEnumerable<ChartsDataModel>> DataProperty =
            AvaloniaProperty.Register<Charts, IEnumerable<ChartsDataModel>>(nameof(Data));

        public IEnumerable<ChartsDataModel> ListViewBindingData
        {
            get { return (IEnumerable<ChartsDataModel>)GetValue(ListViewBindingDataProperty); }
            set { SetValue(ListViewBindingDataProperty, value); }
        }

        public static readonly StyledProperty<IEnumerable<ChartsDataModel>> ListViewBindingDataProperty =
            AvaloniaProperty.Register<Charts, IEnumerable<ChartsDataModel>>(nameof(ListViewBindingData));

        /// <summary>
        /// 加载中时占位显示条数
        /// </summary>
        public int LoadingPlaceholderCount
        {
            get { return (int)GetValue(LoadingPlaceholderCountProperty); }
            set { SetValue(LoadingPlaceholderCountProperty, value); }
        }
        public static readonly StyledProperty<int> LoadingPlaceholderCountProperty =
            AvaloniaProperty.Register<Charts, int>(nameof(LoadingPlaceholderCount));

        /// <summary>
        /// 是否在加载中
        /// </summary>
        public bool IsLoading
        {
            get { return (bool)GetValue(IsLoadingProperty); }
            set { SetValue(IsLoadingProperty, value); }
        }

        public static readonly StyledProperty<bool> IsLoadingProperty =
            AvaloniaProperty.Register<Charts, bool>(nameof(IsLoading));

        /// <summary>
        /// 是否可以滚动
        /// </summary>
        public bool IsCanScroll
        {
            get { return (bool)GetValue(IsCanScrollProperty); }
            set { SetValue(IsCanScrollProperty, value); }
        }
        public static readonly StyledProperty<bool> IsCanScrollProperty =
            AvaloniaProperty.Register<Charts, bool>(nameof(IsCanScroll));

        /// <summary>
        /// 点击命令
        /// </summary>
        public ICommand ClickCommand
        {
            get { return GetValue(ClickCommandProperty); }
            set { SetValue(ClickCommandProperty, value); }
        }

        public static readonly StyledProperty<ICommand> ClickCommandProperty =
             AvaloniaProperty.Register<Charts, ICommand>(nameof(ClickCommand));

        /// <summary>
        /// 柱状图信息数据
        /// </summary>
        public List<ChartColumnInfoModel> ColumnInfoList
        {
            get { return (List<ChartColumnInfoModel>)GetValue(ColumnInfoListProperty); }
            set { SetValue(ColumnInfoListProperty, value); }
        }
        public static readonly StyledProperty<List<ChartColumnInfoModel>> ColumnInfoListProperty =
             AvaloniaProperty.Register<Charts, List<ChartColumnInfoModel>>(nameof(ColumnInfoList));


        /// <summary>
        /// 中间值
        /// </summary>
        public string Median
        {
            get { return (string)GetValue(MedianProperty); }
            set { SetValue(MedianProperty, value); }
        }

        public static readonly StyledProperty<string> MedianProperty =
            AvaloniaProperty.Register<Charts, string>(nameof(Median));

        /// <summary>
        /// 最大值
        /// </summary>
        public string Maximum
        {
            get { return (string)GetValue(MaximumProperty); }
            set { SetValue(MaximumProperty, value); }
        }

        public static readonly StyledProperty<string> MaximumProperty =
            AvaloniaProperty.Register<Charts, string>(nameof(Maximum));

        /// <summary>
        /// 单位
        /// </summary>
        public string Unit
        {
            get { return (string)GetValue(UnitProperty); }
            set { SetValue(UnitProperty, value); }
        }
        public static readonly StyledProperty<string> UnitProperty =
           AvaloniaProperty.Register<Charts, string>(nameof(Unit));


        /// <summary>
        /// 总计
        /// </summary>
        public string Total
        {
            get { return (string)GetValue(TotalProperty); }
            set { SetValue(TotalProperty, value); }
        }
        public static readonly StyledProperty<string> TotalProperty =
            AvaloniaProperty.Register<Charts, string>(nameof(Total));

        /// <summary>
        /// 数据值类型
        /// </summary>
        public ChartDataValueType DataValueType
        {
            get { return (ChartDataValueType)GetValue(DataValueTypeProperty); }
            set { SetValue(DataValueTypeProperty, value); }
        }
        public static readonly StyledProperty<ChartDataValueType> DataValueTypeProperty =
          AvaloniaProperty.Register<Charts, ChartDataValueType>(nameof(DataValueType));

        public int NameIndexStart
        {
            get { return (int)GetValue(NameIndexStartProperty); }
            set { SetValue(NameIndexStartProperty, value); }
        }
        public static readonly StyledProperty<int> NameIndexStartProperty =
          AvaloniaProperty.Register<Charts, int>(nameof(NameIndexStart));

        /// <summary>
        /// 是否空数据
        /// </summary>
        public bool IsEmpty
        {
            get { return (bool)GetValue(IsEmptyProperty); }
            set { SetValue(IsEmptyProperty, value); }
        }
        public static readonly StyledProperty<bool> IsEmptyProperty =
             AvaloniaProperty.Register<Charts, bool>(nameof(IsEmpty));


        /// <summary>
        /// 是否显示总计值（仅柱状图有效，默认显示）
        /// </summary>
        public bool IsShowTotal
        {
            get { return (bool)GetValue(IsShowTotalProperty); }
            set { SetValue(IsShowTotalProperty, value); }
        }
        public static readonly StyledProperty<bool> IsShowTotalProperty =
            AvaloniaProperty.Register<Charts, bool>(nameof(IsShowTotal), true);

        /// <summary>
        /// 是否允许选择项(仅柱状图有效,默认不允许)
        /// </summary>
        public bool IsCanColumnSelect
        {
            get { return (bool)GetValue(IsCanColumnSelectProperty); }
            set { SetValue(IsCanColumnSelectProperty, value); }
        }
        public static readonly StyledProperty<bool> IsCanColumnSelectProperty =
             AvaloniaProperty.Register<Charts, bool>(nameof(IsCanColumnSelect));

        /// <summary>
        /// 是否显示搜索(仅列表样式有效,默认不显示)
        /// </summary>
        public bool IsSearch
        {
            get { return (bool)GetValue(IsSearchProperty); }
            set { SetValue(IsSearchProperty, value); }
        }
        public static readonly StyledProperty<bool> IsSearchProperty =
            AvaloniaProperty.Register<Charts, bool>(nameof(IsSearch), false);

        /// <summary>
        /// 选中列索引（仅柱状图有效）
        /// </summary>
        public int ColumnSelectedIndex
        {
            get { return (int)GetValue(ColumnSelectedIndexProperty); }
            set { SetValue(ColumnSelectedIndexProperty, value); }
        }
        public static readonly StyledProperty<int> ColumnSelectedIndexProperty =
           AvaloniaProperty.Register<Charts, int>(nameof(ColumnSelectedIndex));

        /// <summary>
        /// 是否显示徽章(默认不显示)
        /// </summary>
        public bool IsShowBadge
        {
            get { return (bool)GetValue(IsShowBadgeProperty); }
            set { SetValue(IsShowBadgeProperty, value); }
        }

        public static readonly StyledProperty<bool> IsShowBadgeProperty =
             AvaloniaProperty.Register<Charts, bool>(nameof(IsShowBadge), false);

        public bool IsShowValuesPopup
        {
            get { return (bool)GetValue(IsShowValuesPopupProperty); }
            set { SetValue(IsShowValuesPopupProperty, value); }
        }
        public static readonly StyledProperty<bool> IsShowValuesPopupProperty =
            AvaloniaProperty.Register<Charts, bool>(nameof(IsShowValuesPopup));

        public Control ValuesPopupPlacementTarget
        {
            get { return (Control)GetValue(ValuesPopupPlacementTargetProperty); }
            set { SetValue(ValuesPopupPlacementTargetProperty, value); }
        }
        public static readonly StyledProperty<Control> ValuesPopupPlacementTargetProperty =
          AvaloniaProperty.Register<Charts, Control>(nameof(ValuesPopupPlacementTarget));

        public List<ChartColumnInfoModel> ColumnValuesInfoList
        {
            get { return (List<ChartColumnInfoModel>)GetValue(ColumnValuesInfoListProperty); }
            set { SetValue(ColumnValuesInfoListProperty, value); }
        }
        public static readonly StyledProperty<List<ChartColumnInfoModel>> ColumnValuesInfoListProperty =
            AvaloniaProperty.Register<Charts, List<ChartColumnInfoModel>>(nameof(ColumnValuesInfoList));

        public double ValuesPopupHorizontalOffset
        {
            get { return (double)GetValue(ValuesPopupHorizontalOffsetProperty); }
            set { SetValue(ValuesPopupHorizontalOffsetProperty, value); }
        }

        public static readonly StyledProperty<double> ValuesPopupHorizontalOffsetProperty =
             AvaloniaProperty.Register<Charts, double>(nameof(ValuesPopupHorizontalOffset));

        /// <summary>
        /// 数据最大值
        /// </summary>
        public double DataMaximum
        {
            get { return (double)GetValue(DataMaximumProperty); }
            set { SetValue(DataMaximumProperty, value); }
        }
        public static readonly StyledProperty<double> DataMaximumProperty =
          AvaloniaProperty.Register<Charts, double>(nameof(DataMaximum), 0);

        public double DataMaxValue
        {
            get { return (double)GetValue(DataMaxValueProperty); }
            set { SetValue(DataMaxValueProperty, value); }
        }
        public static readonly StyledProperty<double> DataMaxValueProperty =
            AvaloniaProperty.Register<Charts, double>(nameof(DataMaxValue));

        /// <summary>
        /// 是否以堆叠的形式展示（仅柱状图有效）
        /// </summary>
        public bool IsStack
        {
            get { return (bool)GetValue(IsStackProperty); }
            set { SetValue(IsStackProperty, value); }
        }
        public static readonly StyledProperty<bool> IsStackProperty =
             AvaloniaProperty.Register<Charts, bool>(nameof(IsStack));

        /// <summary>
        /// 是否显示分类信息（仅柱状图有效）
        /// </summary>
        public bool IsShowCategory
        {
            get { return (bool)GetValue(IsShowCategoryProperty); }
            set { SetValue(IsShowCategoryProperty, value); }
        }
        public static readonly StyledProperty<bool> IsShowCategoryProperty =
              AvaloniaProperty.Register<Charts, bool>(nameof(IsShowCategory));

        /// <summary>
        /// 图标大小（仅列表样式有效）
        /// </summary>
        public double IconSize
        {
            get { return (double)GetValue(IconSizeProperty); }
            set { SetValue(IconSizeProperty, value); }
        }
        public static readonly StyledProperty<double> IconSizeProperty =
          AvaloniaProperty.Register<Charts, double>(nameof(IconSize), 25);

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            var charts = (change.Sender as Charts);
            if (change.Property == DataProperty && change.OldValue != change.NewValue)
            {
                charts.Render();
            }
            if (change.Property == IsLoadingProperty)
            {
                charts.Render();
            }
            if (change.Property == ColumnSelectedIndexProperty && change.OldValue != change.NewValue)
            {
                charts.SetColBorderActiveBg((int)change.OldValue, (int)change.NewValue);
            }

        }

        /// <summary>
        /// 右键菜单
        /// </summary>
        public ContextMenu ItemMenu
        {
            get { return (ContextMenu)GetValue(ItemMenuProperty); }
            set { SetValue(ItemMenuProperty, value); }
        }
        public static readonly StyledProperty<ContextMenu> ItemMenuProperty =
            AvaloniaProperty.Register<Charts, ContextMenu>(nameof(ItemMenu));

        /// <summary>
        /// 点击项目时发生
        /// </summary>
        public event EventHandler OnItemClick;

        /// <summary>
        /// 容器
        /// </summary>
        private StackPanel _typeATempContainer;
        private WrapPanel CardContainer;
        private Grid MonthContainer;
        private Border RadarContainer;
        private ListBox _listView;
        private Canvas _typeColumnCanvas;
        private Canvas _commonCanvas;

        private Dictionary<int, List<Rectangle>> _typeColValueRectMap;
        private Dictionary<int, Rectangle> _typeColBorderRectMap;

        /// <summary>
        /// 是否在渲染中
        /// </summary>
        private bool isRendering = false;
        /// <summary>
        /// 计算最大值
        /// </summary>
        private double maxValue = 0;

        /// <summary>
        /// 搜索关键字（仅列表样式有效
        /// </summary>
        private string searchKey;

        private Run _countText;

        private TextBox _searchBox;

        protected override Type StyleKeyOverride => typeof(Charts);

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            CardContainer = e.NameScope.Find("CardContainer") as WrapPanel;
            MonthContainer = e.NameScope.Find("MonthContainer") as Grid;
            RadarContainer = e.NameScope.Find("Radar") as Border;
            _listView = e.NameScope.Find("ListView") as ListBox;
            _typeATempContainer = e.NameScope.Find("TypeATempContainer") as StackPanel;
            _commonCanvas = e.NameScope.Find("CommonCanvasContainer") as Canvas;
            _countText = e.NameScope.Find("ACount") as Run;
            _searchBox = e.NameScope.Find("ASearchBox") as TextBox;
            if (ChartsType == ChartsType.Column)
            {
                _typeColumnCanvas = e.NameScope.Find("TypeColumnCanvas") as Canvas;
                _typeColumnCanvas.SizeChanged += _typeColumnCanvas_SizeChanged;
            }
            if (ChartsType == ChartsType.Pie)
            {
                _commonCanvas.SizeChanged += _commonCanvas_SizeChanged;
            }
            Render();
        }

        private void _commonCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            RenderPieStyle();
        }

        private void _typeColumnCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            RenderColumnStyle();
        }

        private void Render()
        {
            if (IsLoading)
            {
                RenderLoadingPlaceholder();
            }
            else
            {
                RenderData();
            }
        }

        private void RenderLoadingPlaceholder()
        {
            if (LoadingPlaceholderCount > 0 && CardContainer != null)
            {
                CardContainer.Children.Clear();
                _typeATempContainer.Children.Clear();

                for (int i = 0; i < LoadingPlaceholderCount; i++)
                {
                    switch (ChartsType)
                    {
                        case ChartsType.List:
                            var item = new ChartsItemTypeList();
                            item.IsLoading = true;
                            _typeATempContainer.Children.Add(item);
                            break;
                        case ChartsType.Card:
                            var card = new ChartsItemTypeCard();
                            card.IsLoading = true;
                            CardContainer.Children.Add(card);
                            break;
                    }
                }

            }
        }

        private void Calculate()
        {
            if (Data == null || ChartsType == ChartsType.Column)
            {
                return;
            }
            //  计算最大值
            //  如果设置了固定的最大值则使用，否则查找数据中的最大值
            maxValue = MaxValueLimit > 0 ? MaxValueLimit : Data.Count() > 0 ? Data.Max(m => m.Value) : 0;

            if (ChartsType == ChartsType.List)
            {
                //不允许最大值小于10，否则效果不好看
                if (maxValue < 10)
                {
                    maxValue = 10;
                }
                //  适当增加最大值
                maxValue = Math.Round(maxValue / 2, MidpointRounding.AwayFromZero) * 2 + 2;
                if (_countText != null)
                {
                    _countText.Text = Data.Count().ToString();
                }
            }

            DataMaxValue = maxValue;
        }

        private void RenderData()
        {
            if (isRendering || CardContainer == null)
            {
                return;
            }


            Calculate();
            CardContainer.Children.Clear();
            MonthContainer.Children.Clear();
            _typeATempContainer.Children.Clear();
            _commonCanvas.Children.Clear();

            RadarContainer.Child = null;
            ListViewBindingData = null;

            if (Data == null || Data.Count() <= 0)
            {
                CardContainer.Children.Add(new EmptyData());
                RadarContainer.Child = new EmptyData();
                _typeATempContainer.Children.Add(new EmptyData());
                _commonCanvas.Children.Add(new EmptyData());

                IsEmpty = true;
                return;
            }
            else
            {
                IsEmpty = false;
            }

            isRendering = true;
            switch (ChartsType)
            {
                case ChartsType.List:
                    RenderListStyle();
                    break;
                case ChartsType.Card:
                    RenderCardStyle();
                    break;
                case ChartsType.Month:
                    RenderMonthStyle();
                    break;
                case ChartsType.Column:
                    RenderColumnStyle();
                    break;
                case ChartsType.Radar:
                    RenderLadarStyle();
                    break;
                case ChartsType.Pie:
                    RenderPieStyle();
                    break;
            }

        }

        #region 渲染列表样式
        private void RenderListStyle()
        {
            ListViewBindingData = Data.OrderByDescending(x => x.Value).ToList();
            if (ShowLimit > 0)
            {
                ListViewBindingData = ListViewBindingData.Take(ShowLimit);
            }

            isRendering = false;
          
            if (_searchBox != null)
            {
                
                _searchBox.TextChanged += SearchBox_TextChanged;
            }

            _listView.PointerPressed += (s, e) =>
            {
                if (e.GetCurrentPoint(_listView).Properties.IsLeftButtonPressed)
                {
                    _listView_MouseLeftButtonUp(s, e);
                }

                if (e.GetCurrentPoint(_listView).Properties.IsRightButtonPressed)
                {
                    _listView_MouseRightButtonUp(s, e);
                }
            };


            if (!IsCanScroll)
            {
                _listView.PointerWheelChanged += _listView_PreviewMouseWheel;
            }
        }
        private void _listView_PreviewMouseWheel(object sender, PointerWheelEventArgs e)
        {
            if (!e.Handled)
            {
                e.Handled = true;
                var parent = (sender as Control)?.Parent as IInputElement;
                parent?.RaiseEvent(e);
            }
        }

        private void _listView_MouseRightButtonUp(object sender, PointerPressedEventArgs e)
        {
            if (_listView.SelectedItem != null)
            {
                if (ItemMenu != null)
                {
                    //ItemMenu.IsOpen = true;
                    ItemMenu.Tag = _listView.SelectedItem;
                }
            }
        }

        private void _listView_MouseLeftButtonUp(object sender, PointerPressedEventArgs e)
        {
            if (_listView.SelectedItem != null)
            {
                OnItemClick?.Invoke(_listView.SelectedItem, null);
                ClickCommand?.Execute(_listView.SelectedItem);
            }
        }


        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var box = sender as TextBlock;
            if (box.Text != searchKey)
            {
                searchKey = box.Text.ToLower();
                HandleSearch();
            }
        }

        private void HandleSearch()
        {
            Task.Run(() =>
            {
                Dispatcher.UIThread.Invoke(() =>
                {
                    var newListData = new List<ChartsDataModel>();

                    foreach (var data in Data)
                    {
                        string content = (data.Name + data.PopupText).ToLower();
                        bool show = content.IndexOf(searchKey) != -1;

                        if (!show)
                        {
                            show = searchKey == "忽略" && data.BadgeList.Where(m => m.Type == ChartBadgeType.Ignore).Any();
                            if (data.BadgeList.Any())
                            {
                                //  搜索徽章名（分类名）
                                show = data.BadgeList.Where(m => m.Name.ToLower().Contains(searchKey)).Any();
                            }
                        }

                        if (show)
                        {
                            newListData.Add(data);
                        }
                    }

                    ListViewBindingData = newListData;
                });

            });
        }
        #endregion

        #region 渲染卡片样式Card
        private void RenderCardStyle()
        {
            var data = Data.OrderByDescending(x => x.Value).Take(ShowLimit).ToList();

            data.Shuffle();

            foreach (var item in data)
            {
                var chartsItem = new ChartsItemTypeCard();
                chartsItem.Data = item;

                //  处理点击事件
                HandleItemClick(chartsItem, item);

                chartsItem.MaxValue = maxValue;
                //chartsItem.ToolTip = item.PopupText;
                CardContainer.Children.Add(chartsItem);
            }
            isRendering = false;

        }
        #endregion

        #region 渲染月份样式
        private void RenderMonthStyle()
        {
            var data = Data.ToList();
            DateTime month = data[0].DateTime;

            int days = DateTime.DaysInMonth(month.Year, month.Month);

            //  绘制网格
            var headerGrid = new Grid();
            headerGrid.Margin = new Thickness(0, 0, 0, 10);

            var dataGrid = new Grid();

            string[] week = { "一", "二", "三", "四", "五", "六", "日" };
            for (int i = 0; i < 7; i++)
            {

                dataGrid.ColumnDefinitions.Add(new ColumnDefinition()
                {
                    Width = new GridLength(1, GridUnitType.Star)
                });
                headerGrid.ColumnDefinitions.Add(new ColumnDefinition()
                {
                    Width = new GridLength(1, GridUnitType.Star)
                });

                //  填充头部
                var header = new TextBlock();
                header.Text = week[i];
                header.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
                header.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center;
                Grid.SetColumn(header, i);
                headerGrid.Children.Add(header);
            }

            for (int i = 0; i < 6; i++)
            {
                dataGrid.RowDefinitions.Add(new RowDefinition()
                {
                    Height = new GridLength(1, GridUnitType.Star)
                });
            }

            //  填充空数据
            for (int i = 0; i < days; i++)
            {
                var date = new DateTime(month.Year, month.Month, i + 1);
                var chartsItem = new ChartsItemTypeMonth();
                chartsItem.Data = new ChartsDataModel()
                {
                    DateTime = date,
                };
                var location = CalGridLocation(date);
                Grid.SetColumn(chartsItem, location[0]);
                Grid.SetRow(chartsItem, location[1]);
                dataGrid.Children.Add(chartsItem);
                Debug.WriteLine((i + 1) + " -> " + location[0] + "," + location[1]);
            }

            //  填充有效数据
            foreach (var item in data)
            {
                if (item.DateTime != null)
                {
                    int day = item.DateTime.Day;

                    Debug.WriteLine("日期：" + day + "号");
                    var chartsItem = new ChartsItemTypeMonth();
                    chartsItem.Data = item;
                    chartsItem.ToolTip = item.PopupText;
                    chartsItem.MaxValue = maxValue;

                    //  处理点击事件
                    HandleItemClick(chartsItem, item);

                    var location = CalGridLocation(item.DateTime);

                    Grid.SetColumn(chartsItem, location[0]);
                    Grid.SetRow(chartsItem, location[1]);
                    dataGrid.Children.Add(chartsItem);
                    //if (ShowLimit > 0 && Container.Children.Count == ShowLimit)
                    //{
                    //    break;
                    //}
                }
            }

            var sp = new StackPanel();
            sp.Children.Add(headerGrid);
            sp.Children.Add(dataGrid);

            MonthContainer.Children.Add(sp);
            isRendering = false;
        }

        private int[] CalGridLocation(DateTime date)
        {
            var res = new int[2];
            //  判断1号是星期几
            int firstDayWeekNum = (int)new DateTime(date.Year, date.Month, 1).DayOfWeek;
            if (firstDayWeekNum == 0)
            {
                firstDayWeekNum = 7;
            }

            int col = 0;
            int row = 0;
            if (firstDayWeekNum == 1)
            {
                col = date.Day - 1;
            }
            else
            {
                col = date.Day + firstDayWeekNum - 2;
            }
            if (col > 6)
            {
                row = (int)col / 7;

                int dayWeekNum = (int)new DateTime(date.Year, date.Month, date.Day).DayOfWeek;
                if (dayWeekNum == 0)
                {
                    dayWeekNum = 7;
                }
                col = dayWeekNum - 1;


            }

            res[0] = col;
            res[1] = row;
            return res;
        }
        #endregion

        #region 渲染柱形图
        private void SetColBorderActiveBg(int oldIndex, int newIndex)
        {
            if (!IsCanColumnSelect || _typeColBorderRectMap == null)
            {
                return;
            }

            if (_typeColBorderRectMap.ContainsKey(oldIndex))
            {
                var oldItem = _typeColBorderRectMap[oldIndex];
                oldItem.Fill = new SolidColorBrush(Colors.Transparent);
            }
            if (_typeColBorderRectMap.ContainsKey(newIndex))
            {
                var background = Application.Current.Resources["ThemeBrush"] as SolidColorBrush;

                var checkItem = _typeColBorderRectMap[newIndex];
                checkItem.Fill = new SolidColorBrush(background.Color) { Opacity = .1 };
            }
        }

        /// <summary>
        /// 渲染柱形图
        /// </summary>
        private void RenderColumnStyle()
        {
            if (_typeColumnCanvas == null || _typeColumnCanvas.Bounds.Height == 0)
            {
                return;
            }

            _typeColumnCanvas.Children.Clear();
            ColumnInfoList = null;
            _typeColValueRectMap = new Dictionary<int, List<Rectangle>>();
            _typeColBorderRectMap = new Dictionary<int, Rectangle>();

            maxValue = 0;
            Maximum = string.Empty;
            Median = string.Empty;
            Total = string.Empty;

            if (Data == null || Data.Count() == 0)
            {
                return;
            }


            double total = 0;
            //  查找最大值
            int colValueCount = Data.FirstOrDefault().Values.Length;
            double[] tempValueArr = new double[colValueCount];

            foreach (var item in Data)
            {

                for (int i = 0; i < colValueCount; i++)
                {
                    tempValueArr[i] += item.Values[i];
                }

                //  查找最大值
                double max = item.Values.Max();
                if (max > maxValue)
                {
                    maxValue = max;
                }

                total += item.Values.Sum();

            }

            if (DataMaximum > 0)
            {
                maxValue = DataMaximum;
            }
            else
            {
                if (IsStack)
                {
                    maxValue = tempValueArr.Max();
                }
            }
            if (maxValue == 0)
            {
                maxValue = 10;
            }

            Maximum = Covervalue(maxValue);
            Median = Covervalue((maxValue / 2));
            Total = Covervalue(total);

            //  列数
            int columns = Data.FirstOrDefault().Values.Length;


            //  间距
            int margin = 5;

            if (columns <= 7)
            {
                margin = 25;
            }
            else if (columns <= 12)
            {
                margin = 15;
            }
            else if (columns >= 20)
            {
                margin = 2;
            }

            int columnCount = Data.Count();
            var list = Data.ToList();

            //  列名高度
            const double colNameHeight = 30;
            //  列名下边距
            const double colNameBottomMargin = 5;
            //  画布高度
            double canvasHeight = _typeColumnCanvas.Bounds.Height - colNameHeight - colNameBottomMargin;
            //  画布宽度
            double canvasWidth = _typeColumnCanvas.Bounds.Width;
            //  边框宽度
            double columnBorderWidth = _typeColumnCanvas.Bounds.Width / columns;
            //  列值宽度
            double colValueRectWidth = _typeColumnCanvas.Bounds.Width / columns - (margin * 2);

            for (int i = 0; i < columns; i++)
            {
                //  绘制列边框
                var columnBorder = new Rectangle();
                columnBorder.Width = columnBorderWidth;
                columnBorder.Height = canvasHeight;
                columnBorder.Fill = new SolidColorBrush(Colors.Transparent);
                Canvas.SetLeft(columnBorder, i * columnBorderWidth);
                Canvas.SetTop(columnBorder, colNameBottomMargin);
                columnBorder.ZIndex = 999;
                _typeColumnCanvas.Children.Add(columnBorder);
                _typeColBorderRectMap.Add(i, columnBorder);

                //  列名
                string colName = list[0].ColumnNames != null && list[0].ColumnNames.Length > 0 ? list[0].ColumnNames[i] : (i + NameIndexStart).ToString();
                //  绘制列名
                TextBlock colNameText = new TextBlock();
                colNameText.TextAlignment = TextAlignment.Center;
                colNameText.Width = columnBorderWidth;
                colNameText.FontSize = 12;
                colNameText.Text = colName;
                colNameText.Foreground = UI.Base.Color.Colors.GetFromString("#FF8A8A8A");

                Canvas.SetLeft(colNameText, i * columnBorderWidth);
                Canvas.SetBottom(colNameText, colNameBottomMargin);
                _typeColumnCanvas.Children.Add(colNameText);


                var valuesPopupList = new List<ChartColumnInfoModel>();

                _typeColValueRectMap.Add(i, new List<Rectangle>());

                for (int di = 0; di < columnCount; di++)
                {
                    var item = list[di];
                    //string colColor = item.Color == null ? UI.Base.Color.Colors.MainColors[di] : item.Color;
                    string themeColor = Application.Current.Resources["ThemeColor"].ToString();
                    string colColor = item.Color == null ? themeColor : item.Color;
                    double value = item.Values[i];


                    if (value > 0)
                    {
                        //  绘制列
                        var colValueRect = new Rectangle();
                        colValueRect.Width = colValueRectWidth;
                        colValueRect.Height = value / maxValue * canvasHeight;
                        colValueRect.Fill = UI.Base.Color.Colors.GetFromString(colColor);
                        if (!IsStack)
                        {
                            colValueRect.RadiusX = 4;
                            colValueRect.RadiusY = 4;
                        }
                        Canvas.SetLeft(colValueRect, i * columnBorderWidth + margin);
                        Canvas.SetBottom(colValueRect, colNameHeight);

                        _typeColumnCanvas.Children.Add(colValueRect);
                        _typeColValueRectMap[i].Add(colValueRect);

                        //  列分类统计数据
                        valuesPopupList.Add(new ChartColumnInfoModel()
                        {
                            Color = item.Color,
                            Name = item.Name,
                            Icon = item.Icon,
                            Text = Covervalue(value) + Unit,
                            Value = value,
                        });
                    }
                }
                var index = i;

                columnBorder.PointerPressed += (e, c) =>
                {
                    ColumnValuesInfoList = valuesPopupList.OrderByDescending(m => m.Value).ToList();
                    ValuesPopupPlacementTarget = columnBorder;
                    IsShowValuesPopup = valuesPopupList.Count > 0;

                    ValuesPopupHorizontalOffset = -17.5 + (columnBorder.Bounds.Width / 2);

                    if (ColumnSelectedIndex != index)
                    {
                        var themeBrush = Application.Current.Resources["ThemeBrush"] as SolidColorBrush;
                        columnBorder.Fill = new SolidColorBrush(themeBrush.Color) { Opacity = .05 };
                    }
                };
                columnBorder.PointerReleased += (e, c) =>
                {
                    IsShowValuesPopup = false;
                    if (ColumnSelectedIndex != index)
                    {
                        columnBorder.Fill = new SolidColorBrush(Colors.Transparent);

                    }
                };
                if (IsCanColumnSelect)
                {
                    columnBorder.PointerPressed += (e, c) =>
                    {
                        ColumnSelectedIndex = index;
                    };
                }
            }
            //  调整列值 zindex
            foreach (var item in _typeColValueRectMap)
            {
                var rectList = IsStack ? item.Value : item.Value.OrderByDescending(m => m.Height).ToList();
                for (int i = 0; i < rectList.Count; i++)
                {
                    var rect = rectList[i];
                    if (IsStack)
                    {
                        double marginBottom = 0;
                        if (i > 0)
                        {
                            var lastRect = rectList[i - 1];
                            marginBottom = Canvas.GetBottom(lastRect) + lastRect.Height;
                            Canvas.SetBottom(rect, marginBottom);
                        }
                    }
                    else
                    {
                        rect.ZIndex = i;
                    }
                }
            }


            //  最高
            var topValueText = new TextBlock();
            topValueText.Text = Maximum;
            topValueText.FontSize = 12;
            topValueText.Foreground = UI.Base.Color.Colors.GetFromString("#ccc");
            //topValueText.ToolTip = "最高值";
            var topValueTextSize = UIHelper.MeasureString(topValueText);
            topValueText.ZIndex = 1000;
            Canvas.SetRight(topValueText, 0);
            Canvas.SetTop(topValueText, colNameBottomMargin - topValueTextSize.Height / 2);
            _typeColumnCanvas.Children.Add(topValueText);

            var topValueLine = new Line
            {
                Stroke = new SolidColorBrush(Color.Parse("#ccc")),
                StrokeDashArray = [ 2, 5 ],
                StartPoint = new Point(colNameBottomMargin, colNameBottomMargin),
                EndPoint = new Point(canvasWidth - colNameBottomMargin - topValueTextSize.Width, colNameBottomMargin),
                StrokeThickness = 1
            };
            _typeColumnCanvas.Children.Add(topValueLine);

            //  中间值
            double midY = (maxValue / 2) / maxValue * canvasHeight + colNameBottomMargin;

            var midValueText = new TextBlock();
            midValueText.Text = Median;
            midValueText.FontSize = 12;
            midValueText.Foreground = UI.Base.Color.Colors.GetFromString("#ccc");
            //midValueText.ToolTip = "中间值";

            var midValueTextSize = UIHelper.MeasureString(midValueText);
            //Panel.SetZIndex(midValueText, 1000);
            midValueText.ZIndex = 1000;
            Canvas.SetRight(midValueText, 0);
            Canvas.SetTop(midValueText, midY - midValueTextSize.Height / 2);
            _typeColumnCanvas.Children.Add(midValueText);

            var midValueLine = new Line
            {
                Stroke = UI.Base.Color.Colors.GetFromString("#ccc"),
                StrokeDashArray = [2,5],
                StrokeThickness = 1,
                StartPoint = new Point(colNameBottomMargin, midY),
                EndPoint = new Point(canvasWidth - colNameBottomMargin - midValueTextSize.Width, midY),

            };
            _typeColumnCanvas.Children.Add(midValueLine);


            //  平均
            double avg = tempValueArr.Average();
            double avgY = (avg) / maxValue * canvasHeight;

            var avgValueText = new TextBlock();
            avgValueText.Text = Covervalue(avg);
            avgValueText.FontSize = 12;
            avgValueText.Foreground = UI.Base.Color.Colors.GetFromString(StateData.ThemeColor);
            //avgValueText.ToolTip = "平均值";
            var avgValueTextSize = UIHelper.MeasureString(avgValueText);
            avgValueText.ZIndex = 1000;
            Canvas.SetRight(avgValueText, 0);
            Canvas.SetBottom(avgValueText, avgY + colNameHeight - avgValueTextSize.Height / 2);
            _typeColumnCanvas.Children.Add(avgValueText);

            var avgValueLine = new Line
            {
                Stroke = UI.Base.Color.Colors.GetFromString(StateData.ThemeColor),
                StrokeDashArray = [2,5],
                StrokeThickness = 1,
            };
            avgValueLine.StartPoint = new Point(colNameBottomMargin, canvasHeight - avgY + colNameBottomMargin + avgValueLine.StrokeThickness);
            avgValueLine.EndPoint = new Point(canvasWidth - colNameBottomMargin - avgValueTextSize.Width, 
                canvasHeight - avgY + colNameBottomMargin + avgValueLine.StrokeThickness);
            _typeColumnCanvas.Children.Add(avgValueLine);

            //  组装分类统计数据
            var infoList = new List<ChartColumnInfoModel>();
            list = list.OrderByDescending(m => m.Values.Sum()).ToList();
            foreach (var item in list)
            {
                infoList.Add(new ChartColumnInfoModel()
                {
                    Color = item.Color,
                    Name = item.Name,
                    Icon = item.Icon,
                    Text = Covervalue(item.Values.Sum()) + Unit
                });
            }
            ColumnInfoList = infoList;
            isRendering = false;
        }
        #endregion

        #region 渲染雷达图
        private void RenderLadarStyle()
        {
            if (Data.Count() <= 2)
            {
                RadarContainer.Child = new EmptyData();
                isRendering = false;
                return;
            }
            //  最大值
            double total = 0;
            //  查找最大值
            foreach (var item in Data)
            {
                //  查找最大值
                double max = item.Values.Sum();
                if (max > maxValue)
                {
                    maxValue = max;
                }

                total += item.Values.Sum();

            }
            //maxValue = total;

            if (DataMaximum > 0)
            {
                maxValue = DataMaximum;
            }
            var radar = new ChartsItemTypeRadar();
            radar.Data = Data.OrderBy(m => m.Values.Sum()).ToList();
            radar.MaxValue = maxValue;
            RadarContainer.Child = radar;

            isRendering = false;
        }
        #endregion

        #region 饼图
        private void RenderPieStyle()
        {
            if (_commonCanvas == null)
            {
                isRendering = false;
                return;
            }
            _commonCanvas.Children.Clear();
            var item = new ChartsItemTypePie();
            item.Width = _commonCanvas.Bounds.Width;
            item.Height = _commonCanvas.Bounds.Height;
            item.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
            item.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;
            item.Data = Data.OrderBy(m => m.Value).ToList();
            _commonCanvas.Children.Add(item);

            isRendering = false;
        }
        #endregion

        private string Covervalue(double value)
        {
            if (DataValueType == ChartDataValueType.Seconds)
            {
                return Time.ToString((int)value);
            }
            else
            {
                return value.ToString();
            }
        }

        private void HandleItemClick(Control el, ChartsDataModel data)
        {
            el.PointerPressed += (s, e) =>
            {
                if (e.GetCurrentPoint(el).Properties.IsLeftButtonPressed)
                {
                    OnItemClick?.Invoke(data, null);
                    ClickCommand?.Execute(data);
                }

                if (e.GetCurrentPoint(el).Properties.IsRightButtonPressed)
                {
                    if (ItemMenu != null)
                    {
                        //ItemMenu.IsOpen = true;
                        ItemMenu.Tag = data;
                    }
                }
            };
        }

    }
}

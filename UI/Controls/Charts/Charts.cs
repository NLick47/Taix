using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Core.Librarys;
using UI.Controls.Base;
using UI.Controls.Charts.Model;
using UI.Controls.Input;

namespace UI.Controls.Charts;

public class Charts : TemplatedControl
{
    public static readonly StyledProperty<ChartsType> ChartsTypeProperty =
        AvaloniaProperty.Register<Charts, ChartsType>(nameof(ChartsType));

    public static readonly DirectProperty<Charts, double> MaxValueLimitProperty =
        AvaloniaProperty.RegisterDirect<Charts, double>(
            nameof(MaxValueLimit),
            o => o.MaxValueLimit,
            (o, v) => o.MaxValueLimit = v);

    public static readonly DirectProperty<Charts, int> ShowLimitProperty =
        AvaloniaProperty.RegisterDirect<Charts, int>(
            nameof(ShowLimit),
            o => o.ShowLimit,
            (o, v) => o.ShowLimit = v);

    public static readonly DirectProperty<Charts, IEnumerable<ChartsDataModel>> DataProperty =
        AvaloniaProperty.RegisterDirect<Charts, IEnumerable<ChartsDataModel>>(
            nameof(Data),
            o => o.Data,
            (o, v) => o.Data = v);

    public static readonly DirectProperty<Charts, IEnumerable<ChartsDataModel>> ListViewBindingDataProperty =
        AvaloniaProperty.RegisterDirect<Charts, IEnumerable<ChartsDataModel>>(
            nameof(ListViewBindingData),
            o => o.ListViewBindingData,
            (o, v) => o.ListViewBindingData = v);

    public static readonly DirectProperty<Charts, int> LoadingPlaceholderCountProperty =
        AvaloniaProperty.RegisterDirect<Charts, int>(
            nameof(LoadingPlaceholderCount),
            o => o.LoadingPlaceholderCount,
            (o, v) => o.LoadingPlaceholderCount = v);

    public static readonly DirectProperty<Charts, bool> IsLoadingProperty =
        AvaloniaProperty.RegisterDirect<Charts, bool>(
            nameof(IsLoading),
            o => o.IsLoading,
            (o, v) => o.IsLoading = v);

    public static readonly DirectProperty<Charts, bool> IsCanScrollProperty =
        AvaloniaProperty.RegisterDirect<Charts, bool>(
            nameof(IsCanScroll),
            o => o.IsCanScroll,
            (o, v) => o.IsCanScroll = v);

    public static readonly DirectProperty<Charts, ICommand> ClickCommandProperty =
        AvaloniaProperty.RegisterDirect<Charts, ICommand>(
            nameof(ClickCommand),
            o => o.ClickCommand,
            (o, v) => o.ClickCommand = v);

    public static readonly DirectProperty<Charts, List<ChartColumnInfoModel>> ColumnInfoListProperty =
        AvaloniaProperty.RegisterDirect<Charts, List<ChartColumnInfoModel>>(
            nameof(ColumnInfoList),
            o => o.ColumnInfoList,
            (o, v) => o.ColumnInfoList = v);

    public static readonly DirectProperty<Charts, string> MedianProperty =
        AvaloniaProperty.RegisterDirect<Charts, string>(
            nameof(Median),
            o => o.Median,
            (o, v) => o.Median = v);

    public static readonly DirectProperty<Charts, string> MaximumProperty =
        AvaloniaProperty.RegisterDirect<Charts, string>(
            nameof(Maximum),
            o => o.Maximum,
            (o, v) => o.Maximum = v);

    public static readonly DirectProperty<Charts, string> UnitProperty =
        AvaloniaProperty.RegisterDirect<Charts, string>(
            nameof(Unit),
            o => o.Unit,
            (o, v) => o.Unit = v);

    public static readonly DirectProperty<Charts, string> TotalProperty =
        AvaloniaProperty.RegisterDirect<Charts, string>(
            nameof(Total),
            o => o.Total,
            (o, v) => o.Total = v);

    public static readonly DirectProperty<Charts, ChartDataValueType> DataValueTypeProperty =
        AvaloniaProperty.RegisterDirect<Charts, ChartDataValueType>(
            nameof(DataValueType),
            o => o.DataValueType,
            (o, v) => o.DataValueType = v);

    public static readonly DirectProperty<Charts, int> NameIndexStartProperty =
        AvaloniaProperty.RegisterDirect<Charts, int>(
            nameof(NameIndexStart),
            o => o.NameIndexStart,
            (o, v) => o.NameIndexStart = v);

    public static readonly DirectProperty<Charts, bool> IsEmptyProperty =
        AvaloniaProperty.RegisterDirect<Charts, bool>(
            nameof(IsEmpty),
            o => o.IsEmpty,
            (o, v) => o.IsEmpty = v);

    public static readonly DirectProperty<Charts, bool> IsShowTotalProperty =
        AvaloniaProperty.RegisterDirect<Charts, bool>(
            nameof(IsShowTotal),
            o => o.IsShowTotal,
            (o, v) => o.IsShowTotal = v);

    public static readonly DirectProperty<Charts, bool> IsCanColumnSelectProperty =
        AvaloniaProperty.RegisterDirect<Charts, bool>(
            nameof(IsCanColumnSelect),
            o => o.IsCanColumnSelect,
            (o, v) => o.IsCanColumnSelect = v);

    public static readonly DirectProperty<Charts, bool> IsSearchProperty =
        AvaloniaProperty.RegisterDirect<Charts, bool>(
            nameof(IsSearch),
            o => o.IsSearch,
            (o, v) => o.IsSearch = v);

    public static readonly DirectProperty<Charts, int> ColumnSelectedIndexProperty =
        AvaloniaProperty.RegisterDirect<Charts, int>(
            nameof(ColumnSelectedIndex),
            o => o.ColumnSelectedIndex,
            (o, v) => o.ColumnSelectedIndex = v);

    public static readonly DirectProperty<Charts, bool> IsShowBadgeProperty =
        AvaloniaProperty.RegisterDirect<Charts, bool>(
            nameof(IsShowBadge),
            o => o.IsShowBadge,
            (o, v) => o.IsShowBadge = v);

    public static readonly DirectProperty<Charts, bool> IsShowValuesPopupProperty =
        AvaloniaProperty.RegisterDirect<Charts, bool>(
            nameof(IsShowValuesPopup),
            o => o.IsShowValuesPopup,
            (o, v) => o.IsShowValuesPopup = v);

    public static readonly DirectProperty<Charts, Control> ValuesPopupPlacementTargetProperty =
        AvaloniaProperty.RegisterDirect<Charts, Control>(
            nameof(ValuesPopupPlacementTarget),
            o => o.ValuesPopupPlacementTarget,
            (o, v) => o.ValuesPopupPlacementTarget = v);

    public static readonly DirectProperty<Charts, List<ChartColumnInfoModel>> ColumnValuesInfoListProperty =
        AvaloniaProperty.RegisterDirect<Charts, List<ChartColumnInfoModel>>(
            nameof(ColumnValuesInfoList),
            o => o.ColumnValuesInfoList,
            (o, v) => o.ColumnValuesInfoList = v);

    public static readonly DirectProperty<Charts, double> ValuesPopupHorizontalOffsetProperty =
        AvaloniaProperty.RegisterDirect<Charts, double>(
            nameof(ValuesPopupHorizontalOffset),
            o => o.ValuesPopupHorizontalOffset,
            (o, v) => o.ValuesPopupHorizontalOffset = v);

    public static readonly DirectProperty<Charts, double> DataMaximumProperty =
        AvaloniaProperty.RegisterDirect<Charts, double>(
            nameof(DataMaximum),
            o => o.DataMaximum,
            (o, v) => o.DataMaximum = v);

    public static readonly DirectProperty<Charts, double> DataMaxValueProperty =
        AvaloniaProperty.RegisterDirect<Charts, double>(
            nameof(DataMaxValue),
            o => o.DataMaxValue,
            (o, v) => o.DataMaxValue = v);

    public static readonly DirectProperty<Charts, bool> IsStackProperty =
        AvaloniaProperty.RegisterDirect<Charts, bool>(
            nameof(IsStack),
            o => o.IsStack,
            (o, v) => o.IsStack = v);

    public static readonly DirectProperty<Charts, bool> IsShowCategoryProperty =
        AvaloniaProperty.RegisterDirect<Charts, bool>(
            nameof(IsShowCategory),
            o => o.IsShowCategory,
            (o, v) => o.IsShowCategory = v);

    public static readonly DirectProperty<Charts, double> IconSizeProperty =
        AvaloniaProperty.RegisterDirect<Charts, double>(
            nameof(IconSize),
            o => o.IconSize,
            (o, v) => o.IconSize = v);

    public static readonly DirectProperty<Charts, ContextMenu> ItemMenuProperty =
        AvaloniaProperty.RegisterDirect<Charts, ContextMenu>(
            nameof(ItemMenu),
            o => o.ItemMenu,
            (o, v) => o.ItemMenu = v);

    private ICommand _clickCommand;

    private List<ChartColumnInfoModel> _columnInfoList;

    private int _columnSelectedIndex;

    private List<ChartColumnInfoModel> _columnValuesInfoList;
    private Canvas _commonCanvas;

    private Run _countText;

    private IEnumerable<ChartsDataModel> _data;

    private double _dataMaximum;

    private double _dataMaxValue;

    private ChartDataValueType _dataValueType;

    private double _iconSize = 25;

    private bool _isCanColumnSelect;

    private bool _isCanScroll;

    private bool _isEmpty;

    private bool _isLoading;

    private bool _isSearch;

    private bool _isShowBadge;

    private bool _isShowCategory = true;

    private bool _isShowTotal = true;

    private bool _isShowValuesPopup;

    private bool _isStack;

    private ContextMenu _itemMenu;
    private ListBox _listView;

    private IEnumerable<ChartsDataModel> _listViewBindingData;

    private int _loadingPlaceholderCount;

    private string _maximum;

    private double _maxValueLimit;

    private string _median;

    private int _nameIndexStart;

    private TextBox _searchBox;

    private int _showLimit;

    private string _total;

    /// <summary>
    ///     容器
    /// </summary>
    private StackPanel _typeATempContainer;

    private Dictionary<int, Rectangle> _typeColBorderRectMap;
    private Canvas _typeColumnCanvas;

    private Dictionary<int, List<Rectangle>> _typeColValueRectMap;

    private string _unit;

    private double _valuesPopupHorizontalOffset;

    private Control _valuesPopupPlacementTarget;

    private WrapPanel CardContainer;

    /// <summary>
    ///     是否在渲染中
    /// </summary>
    private bool isRendering;

    /// <summary>
    ///     计算最大值
    /// </summary>
    private double maxValue;

    private Grid MonthContainer;
    private Border RadarContainer;

    /// <summary>
    ///     搜索关键字（仅列表样式有效
    /// </summary>
    private string searchKey;

    public ChartsType ChartsType
    {
        get => GetValue(ChartsTypeProperty);
        set => SetValue(ChartsTypeProperty, value);
    }

    public double MaxValueLimit
    {
        get => _maxValueLimit;
        set => SetAndRaise(MaxValueLimitProperty, ref _maxValueLimit, value);
    }

    public int ShowLimit
    {
        get => _showLimit;
        set => SetAndRaise(ShowLimitProperty, ref _showLimit, value);
    }

    public IEnumerable<ChartsDataModel> Data
    {
        get => _data;
        set => SetAndRaise(DataProperty, ref _data, value);
    }

    public IEnumerable<ChartsDataModel> ListViewBindingData
    {
        get => _listViewBindingData;
        set => SetAndRaise(ListViewBindingDataProperty, ref _listViewBindingData, value);
    }

    public int LoadingPlaceholderCount
    {
        get => _loadingPlaceholderCount;
        set => SetAndRaise(LoadingPlaceholderCountProperty, ref _loadingPlaceholderCount, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetAndRaise(IsLoadingProperty, ref _isLoading, value);
    }

    public bool IsCanScroll
    {
        get => _isCanScroll;
        set => SetAndRaise(IsCanScrollProperty, ref _isCanScroll, value);
    }

    public ICommand ClickCommand
    {
        get => _clickCommand;
        set => SetAndRaise(ClickCommandProperty, ref _clickCommand, value);
    }

    public List<ChartColumnInfoModel> ColumnInfoList
    {
        get => _columnInfoList;
        set => SetAndRaise(ColumnInfoListProperty, ref _columnInfoList, value);
    }

    public string Median
    {
        get => _median;
        set => SetAndRaise(MedianProperty, ref _median, value);
    }

    public string Maximum
    {
        get => _maximum;
        set => SetAndRaise(MaximumProperty, ref _maximum, value);
    }

    public string Unit
    {
        get => _unit;
        set => SetAndRaise(UnitProperty, ref _unit, value);
    }

    public string Total
    {
        get => _total;
        set => SetAndRaise(TotalProperty, ref _total, value);
    }

    public ChartDataValueType DataValueType
    {
        get => _dataValueType;
        set => SetAndRaise(DataValueTypeProperty, ref _dataValueType, value);
    }

    public int NameIndexStart
    {
        get => _nameIndexStart;
        set => SetAndRaise(NameIndexStartProperty, ref _nameIndexStart, value);
    }

    public bool IsEmpty
    {
        get => _isEmpty;
        set => SetAndRaise(IsEmptyProperty, ref _isEmpty, value);
    }

    public bool IsShowTotal
    {
        get => _isShowTotal;
        set => SetAndRaise(IsShowTotalProperty, ref _isShowTotal, value);
    }

    public bool IsCanColumnSelect
    {
        get => _isCanColumnSelect;
        set => SetAndRaise(IsCanColumnSelectProperty, ref _isCanColumnSelect, value);
    }

    public bool IsSearch
    {
        get => _isSearch;
        set => SetAndRaise(IsSearchProperty, ref _isSearch, value);
    }

    public int ColumnSelectedIndex
    {
        get => _columnSelectedIndex;
        set => SetAndRaise(ColumnSelectedIndexProperty, ref _columnSelectedIndex, value);
    }

    public bool IsShowBadge
    {
        get => _isShowBadge;
        set => SetAndRaise(IsShowBadgeProperty, ref _isShowBadge, value);
    }

    public bool IsShowValuesPopup
    {
        get => _isShowValuesPopup;
        set => SetAndRaise(IsShowValuesPopupProperty, ref _isShowValuesPopup, value);
    }

    public Control ValuesPopupPlacementTarget
    {
        get => _valuesPopupPlacementTarget;
        set => SetAndRaise(ValuesPopupPlacementTargetProperty, ref _valuesPopupPlacementTarget, value);
    }

    public List<ChartColumnInfoModel> ColumnValuesInfoList
    {
        get => _columnValuesInfoList;
        set => SetAndRaise(ColumnValuesInfoListProperty, ref _columnValuesInfoList, value);
    }

    public double ValuesPopupHorizontalOffset
    {
        get => _valuesPopupHorizontalOffset;
        set => SetAndRaise(ValuesPopupHorizontalOffsetProperty, ref _valuesPopupHorizontalOffset, value);
    }

    public double DataMaximum
    {
        get => _dataMaximum;
        set => SetAndRaise(DataMaximumProperty, ref _dataMaximum, value);
    }

    public double DataMaxValue
    {
        get => _dataMaxValue;
        set => SetAndRaise(DataMaxValueProperty, ref _dataMaxValue, value);
    }

    public bool IsStack
    {
        get => _isStack;
        set => SetAndRaise(IsStackProperty, ref _isStack, value);
    }

    public bool IsShowCategory
    {
        get => _isShowCategory;
        set => SetAndRaise(IsShowCategoryProperty, ref _isShowCategory, value);
    }

    public double IconSize
    {
        get => _iconSize;
        set => SetAndRaise(IconSizeProperty, ref _iconSize, value);
    }

    public ContextMenu ItemMenu
    {
        get => _itemMenu;
        set => SetAndRaise(ItemMenuProperty, ref _itemMenu, value);
    }

    protected override Type StyleKeyOverride => typeof(Charts);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var charts = change.Sender as Charts;
        if (change.Property == DataProperty) charts.Render();

        if (change.Property == IsLoadingProperty) charts.Render();

        if (change.Property == ColumnSelectedIndexProperty)
            charts.SetColBorderActiveBg((int)change.OldValue, (int)change.NewValue);

        if (change.Property == ItemMenuProperty)
        {
            var (oldVal, newVal) = change.GetOldAndNewValue<ContextMenu>();
            if (oldVal != null && _listView != null)
            {
                oldVal.Opening -= OnContextMenuOpening;
                _listView.SelectionChanged -= _listView_SelectionChanged;
            }

            if (newVal != null && _listView != null)
            {
                _listView.ContextMenu = newVal;
                newVal.Opening += OnContextMenuOpening;
                _listView.SelectionChanged += _listView_SelectionChanged;
            }
        }
    }

    /// <summary>
    ///     点击项目时发生
    /// </summary>
    public event EventHandler OnItemClick;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        CardContainer = e.NameScope.Get<WrapPanel>("CardContainer");
        MonthContainer = e.NameScope.Get<Grid>("MonthContainer");
        RadarContainer = e.NameScope.Get<Border>("Radar");
        _listView = e.NameScope.Get<ListBox>("ListView");
        _typeATempContainer = e.NameScope.Get<StackPanel>("TypeATempContainer");
        _commonCanvas = e.NameScope.Get<Canvas>("CommonCanvasContainer");
        _countText = e.NameScope.Get<Run>("ACount");
        _searchBox = e.NameScope.Get<TextBox>("ASearchBox");
        if (ChartsType == ChartsType.Column)
        {
            _typeColumnCanvas = e.NameScope.Get<Canvas>("TypeColumnCanvas");
            _typeColumnCanvas.SizeChanged += _typeColumnCanvas_SizeChanged;
        }

        if (ChartsType == ChartsType.Pie) _commonCanvas.SizeChanged += _commonCanvas_SizeChanged;

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
            RenderLoadingPlaceholder();
        else
            RenderData();
    }

    private void RenderLoadingPlaceholder()
    {
        if (LoadingPlaceholderCount > 0 && CardContainer != null)
        {
            CardContainer.Children.Clear();
            _typeATempContainer.Children.Clear();

            for (var i = 0; i < LoadingPlaceholderCount; i++)
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

    private void Calculate()
    {
        if (Data == null || ChartsType == ChartsType.Column) return;

        //  计算最大值
        //  如果设置了固定的最大值则使用，否则查找数据中的最大值
        maxValue = MaxValueLimit > 0 ? MaxValueLimit : Data.Count() > 0 ? Data.Max(m => m.Value) : 0;

        if (ChartsType == ChartsType.List)
        {
            //不允许最大值小于10，否则效果不好看
            if (maxValue < 10) maxValue = 10;

            //  适当增加最大值
            maxValue = Math.Round(maxValue / 2, MidpointRounding.AwayFromZero) * 2 + 2;
            if (_countText != null) _countText.Text = Data.Count().ToString();
        }

        DataMaxValue = maxValue;
    }

    private void RenderData()
    {
        if (isRendering || CardContainer == null) return;


        Calculate();

        MonthContainer.Children.Clear();
        _typeATempContainer.Children.Clear();
        _commonCanvas.Children.Clear();

        RadarContainer.Child = null;
        ListViewBindingData = null;

        if (Data == null || Data.Count() <= 0)
        {
            CardContainer.Children.Clear();
            CardContainer.Children.Add(new EmptyData());
            RadarContainer.Child = new EmptyData();
            _typeATempContainer.Children.Add(new EmptyData());
            _commonCanvas.Children.Add(new EmptyData());

            IsEmpty = true;
            return;
        }

        IsEmpty = false;

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

    #region 渲染卡片样式Card

    private void RenderCardStyle()
    {
        var data = Data.Take(ShowLimit).ToList();
        if (data.Count > 0 && CardContainer.Children.Any(x => x is EmptyData))
            CardContainer.Children.Remove(CardContainer.Children.First(x => x is EmptyData));

        var chatItemTypeCards = CardContainer.Children
            .Where(control => control is ChartsItemTypeCard)
            .Cast<ChartsItemTypeCard>()
            .ToList();

        var existingCardsDict = chatItemTypeCards.ToDictionary(card => card.Data.Name, card => card);
        var controlsToRemove = chatItemTypeCards
            .Where(card => !data.Any(item => item.Name == card.Data.Name && item.Value == card.Data.Value))
            .ToList();
        foreach (var control in controlsToRemove)
        {
            CardContainer.Children.Remove(control);
            existingCardsDict.Remove(control.Data.Name);
        }

        for (var i = 0; i < data.Count; i++)
        {
            var item = data[i];
            if (existingCardsDict.TryGetValue(item.Name, out var chartsItem))
            {
                if (chartsItem.Data.Value != item.Value)
                {
                    chartsItem.Data = item;
                    HandleItemClick(chartsItem, item);
                    chartsItem.MaxValue = maxValue;
                    ToolTip.SetTip(chartsItem, item.PopupText);
                }
            }
            else
            {
                chartsItem = new ChartsItemTypeCard();
                chartsItem.Data = item;
                HandleItemClick(chartsItem, item);
                chartsItem.MaxValue = maxValue;
                ToolTip.SetTip(chartsItem, item.PopupText);

                if (i < CardContainer.Children.Count)
                    CardContainer.Children.Insert(i, chartsItem);
                else
                    CardContainer.Children.Add(chartsItem);
            }
        }

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
            var max = item.Values.Sum();
            if (max > maxValue) maxValue = max;

            total += item.Values.Sum();
        }
        //maxValue = total;

        if (DataMaximum > 0) maxValue = DataMaximum;

        var radar = new RadarChart();
        radar.Labels = Data.Select(x => x.Name.Length > 4 ? x.Name.Substring(0, 4) : x.Name).ToList();
        radar.Values = Data.Select(x => x.Values.Sum()).ToList();
        radar.MaxValue = maxValue;
        ToolTip.SetTip(radar,
            string.Join("\n", Data.Select(x => x.Name + $" {Time.ToString((int)x.Values.Sum())}")));
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
        if (Data?.Count() == 0)
        {
            _commonCanvas.Children.Add(new EmptyData
            {
                RenderTransform = new TransformGroup
                {
                    Children =
                    [
                        new TranslateTransform(-15, 0),
                        new ScaleTransform(0.8, 0.8)
                    ]
                }
            });
            return;
        }

        var item = new ChartsItemTypePie();
        item.Width = _commonCanvas.Bounds.Width;
        item.Height = _commonCanvas.Bounds.Height;
        item.HorizontalAlignment = HorizontalAlignment.Left;
        item.VerticalAlignment = VerticalAlignment.Top;
        item.Data = Data.OrderBy(m => m.Value).ToList();
        _commonCanvas.Children.Add(item);

        isRendering = false;
    }

    #endregion


    private string Covervalue(double value)
    {
        if (DataValueType == ChartDataValueType.Seconds) return Time.ToString((int)value);

        return value.ToString();
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
                if (ItemMenu != null)
                    //ItemMenu.IsOpen = true;
                    ItemMenu.Tag = data;
        };
    }

    #region 渲染列表样式

    private void RenderListStyle()
    {
        ListViewBindingData = Data.OrderByDescending(x => x.Value).ToList();
        if (ShowLimit > 0) ListViewBindingData = ListViewBindingData.Take(ShowLimit);

        isRendering = false;
        if (ItemMenu != null)
        {
            ItemMenu.Opening -= OnContextMenuOpening;
            _listView.SelectionChanged -= _listView_SelectionChanged;

            _listView.ContextMenu = ItemMenu;
            ItemMenu.Opening += OnContextMenuOpening;
            _listView.SelectionChanged += _listView_SelectionChanged;
        }

        if (_searchBox != null)
        {
            _searchBox.TextChanged -= SearchBox_TextChanged;
            _searchBox.TextChanged += SearchBox_TextChanged;
        }

        _listView.PointerReleased += OnListViewPointerReleased;
    }

    private void _listView_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var list = sender as ListBox;
        if (list != null && e.AddedItems.Count != 0) list.ContextMenu.Tag = e.AddedItems[0];
    }

    private void OnContextMenuOpening(object? sender, CancelEventArgs e)
    {
        var menu = sender as ContextMenu;
        if (string.IsNullOrEmpty(menu?.Tag?.ToString())) e.Cancel = true;
    }

    private void OnListViewPointerReleased(object sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton == MouseButton.Left) _listView_MouseLeftButtonUp(sender, e);
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);

        if (ItemMenu != null)
        {
            _listView.SelectionChanged -= _listView_SelectionChanged;
            ItemMenu.Opening -= OnContextMenuOpening;
        }

        if (ChartsType == ChartsType.Column) _typeColumnCanvas.SizeChanged -= _typeColumnCanvas_SizeChanged;

        if (ChartsType == ChartsType.Pie) _commonCanvas.SizeChanged -= _commonCanvas_SizeChanged;

        _listView.PointerReleased -= OnListViewPointerReleased;
        _searchBox.TextChanged -= SearchBox_TextChanged;
    }

    private void _listView_MouseLeftButtonUp(object sender, PointerReleasedEventArgs e)
    {
        if (_listView.SelectedItem != null)
        {
            OnItemClick?.Invoke(_listView.SelectedItem, null);
            ClickCommand?.Execute(_listView.SelectedItem);
        }
    }


    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var box = sender as InputBox;
        if (box.Text != searchKey)
        {
            searchKey = box.Text.ToLower();
            HandleSearch();
        }
    }

    private async void HandleSearch()
    {
        var data = Data.ToList();
        await Task.Run(() =>
        {
            var searchKeyLower = searchKey.ToLower();
            var newListData = data
                .Where(data =>
                {
                    var content = (data.Name + data.PopupText).ToLower();
                    var show = content.Contains(searchKeyLower);

                    if (!show)
                    {
                        show = ChartBadgeModel.IgnreLanguages.Contains(searchKeyLower) && data.BadgeList != null &&
                               data.BadgeList.Any(m => m.Type == ChartBadgeType.Ignore);
                        if (!show && data.BadgeList != null && data.BadgeList.Any())
                            show = data.BadgeList.Any(m => m.Name.ToLower().Contains(searchKeyLower));
                    }

                    return show;
                })
                .ToList();

            Dispatcher.UIThread.Invoke(() => ListViewBindingData = newListData);
        });
    }

    #endregion

    #region 渲染月份样式

    private void RenderMonthStyle()
    {
        var data = Data.ToList();
        var month = data[0].DateTime;

        var days = DateTime.DaysInMonth(month.Year, month.Month);

        //  绘制网格
        var headerGrid = new Grid();
        headerGrid.Margin = new Thickness(0, 0, 0, 10);

        var dataGrid = new Grid();

        string[] week =
        [
            Application.Current.Resources["Mon"] as string,
            Application.Current.Resources["Tue"] as string,
            Application.Current.Resources["Wed"] as string,
            Application.Current.Resources["Thu"] as string,
            Application.Current.Resources["Fri"] as string,
            Application.Current.Resources["Sat"] as string,
            Application.Current.Resources["Sun"] as string
        ];
        for (var i = 0; i < 7; i++)
        {
            dataGrid.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(1, GridUnitType.Star)
            });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(1, GridUnitType.Star)
            });

            //  填充头部
            var header = new TextBlock();
            header.Text = week[i];
            header.VerticalAlignment = VerticalAlignment.Center;
            header.HorizontalAlignment = HorizontalAlignment.Center;
            Grid.SetColumn(header, i);
            headerGrid.Children.Add(header);
        }

        for (var i = 0; i < 6; i++)
            dataGrid.RowDefinitions.Add(new RowDefinition
            {
                Height = new GridLength(1, GridUnitType.Star)
            });

        //  填充空数据
        for (var i = 0; i < days; i++)
        {
            var date = new DateTime(month.Year, month.Month, i + 1);
            var chartsItem = new ChartsItemTypeMonth();
            chartsItem.Data = new ChartsDataModel
            {
                DateTime = date
            };
            var location = CalGridLocation(date);
            Grid.SetColumn(chartsItem, location[0]);
            Grid.SetRow(chartsItem, location[1]);
            dataGrid.Children.Add(chartsItem);
            Debug.WriteLine(i + 1 + " -> " + location[0] + "," + location[1]);
        }

        //  填充有效数据
        foreach (var item in data)
            if (item.DateTime != null)
            {
                var day = item.DateTime.Day;

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
        var firstDayWeekNum = (int)new DateTime(date.Year, date.Month, 1).DayOfWeek;
        if (firstDayWeekNum == 0) firstDayWeekNum = 7;

        var col = 0;
        var row = 0;
        if (firstDayWeekNum == 1)
            col = date.Day - 1;
        else
            col = date.Day + firstDayWeekNum - 2;

        if (col > 6)
        {
            row = col / 7;

            var dayWeekNum = (int)new DateTime(date.Year, date.Month, date.Day).DayOfWeek;
            if (dayWeekNum == 0) dayWeekNum = 7;

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
        if (!IsCanColumnSelect || _typeColBorderRectMap == null) return;

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
    ///     渲染柱形图
    /// </summary>
    private void RenderColumnStyle()
    {
        if (_typeColumnCanvas == null || _typeColumnCanvas.Bounds.Height == 0) return;

        _typeColumnCanvas.Children.Clear();
        ColumnInfoList = null;
        _typeColValueRectMap = new Dictionary<int, List<Rectangle>>();
        _typeColBorderRectMap = new Dictionary<int, Rectangle>();

        maxValue = 0;
        Maximum = string.Empty;
        Median = string.Empty;
        Total = string.Empty;

        if (Data == null || Data.Count() == 0) return;


        double total = 0;
        //  查找最大值
        var colValueCount = Data.FirstOrDefault().Values.Length;
        var tempValueArr = new double[colValueCount];

        foreach (var item in Data)
        {
            for (var i = 0; i < colValueCount; i++) tempValueArr[i] += item.Values[i];

            //  查找最大值
            var max = item.Values.Max();
            if (max > maxValue) maxValue = max;

            total += item.Values.Sum();
        }

        if (DataMaximum > 0)
        {
            maxValue = DataMaximum;
        }
        else
        {
            if (IsStack) maxValue = tempValueArr.Max();
        }

        if (maxValue == 0) maxValue = 10;

        Maximum = Covervalue(maxValue);
        Median = Covervalue(maxValue / 2);
        Total = Covervalue(total);

        //  列数
        var columns = Data.FirstOrDefault().Values.Length;

        double pa = 0;
        //  间距
        var margin = 5;

        if (columns <= 7)
        {
            margin = 25;
        }
        else if (columns <= 12)
        {
            margin = 15;
            pa = 1;
        }
        else if (columns >= 20)
        {
            margin = 2;
            pa = 16;
        }

        var columnCount = Data.Count();
        var list = Data.ToList();

        //  列名高度
        const double colNameHeight = 30;
        //  列名下边距
        const double colNameBottomMargin = 5;
        //  画布高度
        var canvasHeight = _typeColumnCanvas.Bounds.Height - colNameHeight - colNameBottomMargin;
        //  画布宽度
        var canvasWidth = _typeColumnCanvas.Bounds.Width;
        //  边框宽度
        var columnBorderWidth = _typeColumnCanvas.Bounds.Width / columns;
        //  列值宽度
        var colValueRectWidth = _typeColumnCanvas.Bounds.Width / columns - margin * 2;

        for (var i = 0; i < columns; i++)
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
            var colName = list[0].ColumnNames != null && list[0].ColumnNames.Length > 0
                ? list[0].ColumnNames[i]
                : (i + NameIndexStart).ToString();
            //  绘制列名
            var colNameText = new TextBlock();
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

            for (var di = 0; di < columnCount; di++)
            {
                var item = list[di];
                //string colColor = item.Color == null ? UI.Base.Color.Colors.MainColors[di] : item.Color;
                var themeColor = App.Current.FindResource("ThemeColor");
                var colColor = item.Color == null ? themeColor.ToString() : item.Color;
                var value = item.Values[i];
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
                    valuesPopupList.Add(new ChartColumnInfoModel
                    {
                        Color = item.Color,
                        Name = item.Name,
                        Icon = item.Icon,
                        Text = Covervalue(value) + Unit,
                        Value = value
                    });
                }
            }

            var index = i;

            columnBorder.PointerEntered += (e, c) =>
            {
                var s = valuesPopupList.OrderByDescending(m => m.Value).ToList();
                if (s.Count > 1)
                    s.Add(new ChartColumnInfoModel
                    {
                        Name = "Sum",
                        Text = Covervalue(s.Sum(x => x.Value)) + Unit,
                        Color = "#FF8A8A8A"
                    });

                ColumnValuesInfoList = s;
                ValuesPopupPlacementTarget = columnBorder;
                IsShowValuesPopup = valuesPopupList.Count > 0;

                ValuesPopupHorizontalOffset = columnBorder.Bounds.Width / 2 + pa * margin;

                if (ColumnSelectedIndex != index)
                {
                    var themeBrush = Application.Current.Resources["ThemeBrush"] as SolidColorBrush;
                    columnBorder.Fill = new SolidColorBrush(themeBrush.Color) { Opacity = .05 };
                }
            };
            columnBorder.PointerExited += (e, c) =>
            {
                IsShowValuesPopup = false;
                if (ColumnSelectedIndex != index) columnBorder.Fill = new SolidColorBrush(Colors.Transparent);
            };
            if (IsCanColumnSelect) columnBorder.PointerPressed += (e, c) => { ColumnSelectedIndex = index; };
        }

        //  调整列值 zindex
        foreach (var item in _typeColValueRectMap)
        {
            var rectList = IsStack ? item.Value : item.Value.OrderByDescending(m => m.Height).ToList();
            for (var i = 0; i < rectList.Count; i++)
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
        ToolTip.SetTip(topValueText, "最高值");
        var topValueTextSize = UIHelper.MeasureString(topValueText);
        topValueText.ZIndex = 1000;
        Canvas.SetRight(topValueText, 0);
        Canvas.SetTop(topValueText, colNameBottomMargin - topValueTextSize.Height / 2);
        _typeColumnCanvas.Children.Add(topValueText);

        var topValueLine = new Line
        {
            Stroke = new SolidColorBrush(Color.Parse("#ccc")),
            StrokeDashArray = [2, 5],
            StartPoint = new Point(colNameBottomMargin, colNameBottomMargin),
            EndPoint = new Point(canvasWidth - colNameBottomMargin - topValueTextSize.Width, colNameBottomMargin),
            StrokeThickness = 1
        };
        _typeColumnCanvas.Children.Add(topValueLine);

        //  中间值
        var midY = maxValue / 2 / maxValue * canvasHeight + colNameBottomMargin;

        var midValueText = new TextBlock();
        midValueText.Text = Median;
        midValueText.FontSize = 12;
        midValueText.Foreground = UI.Base.Color.Colors.GetFromString("#ccc");
        ToolTip.SetTip(midValueText, "中间值");
        var midValueTextSize = UIHelper.MeasureString(midValueText);
        midValueText.ZIndex = 1000;
        Canvas.SetRight(midValueText, 0);
        Canvas.SetTop(midValueText, midY - midValueTextSize.Height / 2);
        _typeColumnCanvas.Children.Add(midValueText);

        var midValueLine = new Line
        {
            Stroke = UI.Base.Color.Colors.GetFromString("#ccc"),
            StrokeDashArray = [2, 5],
            StrokeThickness = 1,
            StartPoint = new Point(colNameBottomMargin, midY),
            EndPoint = new Point(canvasWidth - colNameBottomMargin - midValueTextSize.Width, midY)
        };
        _typeColumnCanvas.Children.Add(midValueLine);


        //  平均
        var avg = tempValueArr.Average();
        var avgY = avg / maxValue * canvasHeight;

        var avgValueText = new TextBlock();
        avgValueText.Text = Covervalue(avg);
        avgValueText.FontSize = 12;
        avgValueText.Foreground = UI.Base.Color.Colors.GetFromString(StateData.ThemeColor);
        ToolTip.SetTip(avgValueText, "平均值");
        var avgValueTextSize = UIHelper.MeasureString(avgValueText);
        avgValueText.ZIndex = 1000;
        Canvas.SetRight(avgValueText, 0);
        Canvas.SetBottom(avgValueText, avgY + colNameHeight - avgValueTextSize.Height / 2);
        _typeColumnCanvas.Children.Add(avgValueText);

        var avgValueLine = new Line
        {
            Stroke = UI.Base.Color.Colors.GetFromString(StateData.ThemeColor),
            StrokeDashArray = [2, 5],
            StrokeThickness = 1
        };
        avgValueLine.StartPoint = new Point(colNameBottomMargin,
            canvasHeight - avgY + colNameBottomMargin + avgValueLine.StrokeThickness);
        avgValueLine.EndPoint = new Point(canvasWidth - colNameBottomMargin - avgValueTextSize.Width,
            canvasHeight - avgY + colNameBottomMargin + avgValueLine.StrokeThickness);
        _typeColumnCanvas.Children.Add(avgValueLine);

        //  组装分类统计数据
        var infoList = new List<ChartColumnInfoModel>();
        list = list.OrderByDescending(m => m.Values.Sum()).ToList();
        foreach (var item in list)
            infoList.Add(new ChartColumnInfoModel
            {
                Color = item.Color,
                Name = item.Name,
                Icon = item.Icon,
                Text = Covervalue(item.Values.Sum()) + Unit
            });

        ColumnInfoList = infoList;
        isRendering = false;
    }

    #endregion
}
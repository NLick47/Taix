using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Taix.Client.Controls.Base;
using Taix.Client.Controls.Charts.Model;
using Taix.Client.Shared.Librarys;

namespace Taix.Client.Controls.Charts;

public class ColumnChart : TemplatedControl
{
    public static readonly DirectProperty<ColumnChart, IEnumerable<ChartsDataModel>> DataProperty =
        AvaloniaProperty.RegisterDirect<ColumnChart, IEnumerable<ChartsDataModel>>(
            nameof(Data), o => o.Data, (o, v) => o.Data = v);

    public static readonly DirectProperty<ColumnChart, double> DataMaximumProperty =
        AvaloniaProperty.RegisterDirect<ColumnChart, double>(
            nameof(DataMaximum), o => o.DataMaximum, (o, v) => o.DataMaximum = v);

    public static readonly DirectProperty<ColumnChart, int> NameIndexStartProperty =
        AvaloniaProperty.RegisterDirect<ColumnChart, int>(
            nameof(NameIndexStart), o => o.NameIndexStart, (o, v) => o.NameIndexStart = v);

    public static readonly DirectProperty<ColumnChart, bool> IsStackProperty =
        AvaloniaProperty.RegisterDirect<ColumnChart, bool>(
            nameof(IsStack), o => o.IsStack, (o, v) => o.IsStack = v);

    public static readonly DirectProperty<ColumnChart, bool> IsShowTotalProperty =
        AvaloniaProperty.RegisterDirect<ColumnChart, bool>(
            nameof(IsShowTotal), o => o.IsShowTotal, (o, v) => o.IsShowTotal = v);

    public static readonly DirectProperty<ColumnChart, bool> IsCanColumnSelectProperty =
        AvaloniaProperty.RegisterDirect<ColumnChart, bool>(
            nameof(IsCanColumnSelect), o => o.IsCanColumnSelect, (o, v) => o.IsCanColumnSelect = v);

    public static readonly DirectProperty<ColumnChart, int> ColumnSelectedIndexProperty =
        AvaloniaProperty.RegisterDirect<ColumnChart, int>(
            nameof(ColumnSelectedIndex), o => o.ColumnSelectedIndex, (o, v) => o.ColumnSelectedIndex = v);

    public static readonly DirectProperty<ColumnChart, bool> IsShowCategoryProperty =
        AvaloniaProperty.RegisterDirect<ColumnChart, bool>(
            nameof(IsShowCategory), o => o.IsShowCategory, (o, v) => o.IsShowCategory = v);

    public static readonly DirectProperty<ColumnChart, ChartDataValueType> DataValueTypeProperty =
        AvaloniaProperty.RegisterDirect<ColumnChart, ChartDataValueType>(
            nameof(DataValueType), o => o.DataValueType, (o, v) => o.DataValueType = v);

    public static readonly DirectProperty<ColumnChart, string> TotalProperty =
        AvaloniaProperty.RegisterDirect<ColumnChart, string>(
            nameof(Total), o => o.Total, (o, v) => o.Total = v);

    public static readonly DirectProperty<ColumnChart, string> UnitProperty =
        AvaloniaProperty.RegisterDirect<ColumnChart, string>(
            nameof(Unit), o => o.Unit, (o, v) => o.Unit = v);

    public static readonly DirectProperty<ColumnChart, string> MaximumProperty =
        AvaloniaProperty.RegisterDirect<ColumnChart, string>(
            nameof(Maximum), o => o.Maximum, (o, v) => o.Maximum = v);

    public static readonly DirectProperty<ColumnChart, string> MedianProperty =
        AvaloniaProperty.RegisterDirect<ColumnChart, string>(
            nameof(Median), o => o.Median, (o, v) => o.Median = v);

    public static readonly DirectProperty<ColumnChart, List<ChartColumnInfoModel>> ColumnInfoListProperty =
        AvaloniaProperty.RegisterDirect<ColumnChart, List<ChartColumnInfoModel>>(
            nameof(ColumnInfoList), o => o.ColumnInfoList, (o, v) => o.ColumnInfoList = v);

    public static readonly DirectProperty<ColumnChart, List<ChartColumnInfoModel>> ColumnValuesInfoListProperty =
        AvaloniaProperty.RegisterDirect<ColumnChart, List<ChartColumnInfoModel>>(
            nameof(ColumnValuesInfoList), o => o.ColumnValuesInfoList, (o, v) => o.ColumnValuesInfoList = v);

    public static readonly DirectProperty<ColumnChart, bool> IsShowValuesPopupProperty =
        AvaloniaProperty.RegisterDirect<ColumnChart, bool>(
            nameof(IsShowValuesPopup), o => o.IsShowValuesPopup, (o, v) => o.IsShowValuesPopup = v);

    public static readonly DirectProperty<ColumnChart, Control> ValuesPopupPlacementTargetProperty =
        AvaloniaProperty.RegisterDirect<ColumnChart, Control>(
            nameof(ValuesPopupPlacementTarget), o => o.ValuesPopupPlacementTarget, (o, v) => o.ValuesPopupPlacementTarget = v);

    public static readonly DirectProperty<ColumnChart, double> ValuesPopupHorizontalOffsetProperty =
        AvaloniaProperty.RegisterDirect<ColumnChart, double>(
            nameof(ValuesPopupHorizontalOffset), o => o.ValuesPopupHorizontalOffset, (o, v) => o.ValuesPopupHorizontalOffset = v);

    private IEnumerable<ChartsDataModel> _data = [];
    private double _dataMaximum;
    private int _nameIndexStart;
    private bool _isStack;
    private bool _isShowTotal = true;
    private bool _isCanColumnSelect;
    private int _columnSelectedIndex = -1;
    private bool _isShowCategory = true;
    private ChartDataValueType _dataValueType;
    private string _total = string.Empty;
    private string _unit = string.Empty;
    private string _maximum = string.Empty;
    private string _median = string.Empty;
    private List<ChartColumnInfoModel> _columnInfoList;
    private List<ChartColumnInfoModel> _columnValuesInfoList;
    private bool _isShowValuesPopup;
    private Control _valuesPopupPlacementTarget;
    private double _valuesPopupHorizontalOffset;

    private Canvas _typeColumnCanvas;
    private Dictionary<int, Rectangle> _typeColBorderRectMap = new();
    private Dictionary<int, List<Rectangle>> _typeColValueRectMap = new();
    private bool _isRendering;

    public IEnumerable<ChartsDataModel> Data { get => _data; set => SetAndRaise(DataProperty, ref _data, value); }
    public double DataMaximum { get => _dataMaximum; set => SetAndRaise(DataMaximumProperty, ref _dataMaximum, value); }
    public int NameIndexStart { get => _nameIndexStart; set => SetAndRaise(NameIndexStartProperty, ref _nameIndexStart, value); }
    public bool IsStack { get => _isStack; set => SetAndRaise(IsStackProperty, ref _isStack, value); }
    public bool IsShowTotal { get => _isShowTotal; set => SetAndRaise(IsShowTotalProperty, ref _isShowTotal, value); }
    public bool IsCanColumnSelect { get => _isCanColumnSelect; set => SetAndRaise(IsCanColumnSelectProperty, ref _isCanColumnSelect, value); }
    public int ColumnSelectedIndex { get => _columnSelectedIndex; set => SetAndRaise(ColumnSelectedIndexProperty, ref _columnSelectedIndex, value); }
    public bool IsShowCategory { get => _isShowCategory; set => SetAndRaise(IsShowCategoryProperty, ref _isShowCategory, value); }
    public ChartDataValueType DataValueType { get => _dataValueType; set => SetAndRaise(DataValueTypeProperty, ref _dataValueType, value); }
    public string Total { get => _total; set => SetAndRaise(TotalProperty, ref _total, value); }
    public string Unit { get => _unit; set => SetAndRaise(UnitProperty, ref _unit, value); }
    public string Maximum { get => _maximum; set => SetAndRaise(MaximumProperty, ref _maximum, value); }
    public string Median { get => _median; set => SetAndRaise(MedianProperty, ref _median, value); }
    public List<ChartColumnInfoModel> ColumnInfoList { get => _columnInfoList; set => SetAndRaise(ColumnInfoListProperty, ref _columnInfoList, value); }
    public List<ChartColumnInfoModel> ColumnValuesInfoList { get => _columnValuesInfoList; set => SetAndRaise(ColumnValuesInfoListProperty, ref _columnValuesInfoList, value); }
    public bool IsShowValuesPopup { get => _isShowValuesPopup; set => SetAndRaise(IsShowValuesPopupProperty, ref _isShowValuesPopup, value); }
    public Control ValuesPopupPlacementTarget { get => _valuesPopupPlacementTarget; set => SetAndRaise(ValuesPopupPlacementTargetProperty, ref _valuesPopupPlacementTarget, value); }
    public double ValuesPopupHorizontalOffset { get => _valuesPopupHorizontalOffset; set => SetAndRaise(ValuesPopupHorizontalOffsetProperty, ref _valuesPopupHorizontalOffset, value); }

    protected override Type StyleKeyOverride => typeof(ColumnChart);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _typeColumnCanvas = e.NameScope.Get<Canvas>("TypeColumnCanvas");
        _typeColumnCanvas.SizeChanged += OnCanvasSizeChanged;
        Render();
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        _isRendering = false;
        if (_typeColumnCanvas != null)
            _typeColumnCanvas.SizeChanged -= OnCanvasSizeChanged;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DataProperty || change.Property == DataMaximumProperty ||
            change.Property == IsStackProperty || change.Property == NameIndexStartProperty)
            Render();

        if (change.Property == ColumnSelectedIndexProperty)
            SetColBorderActiveBg((int)change.OldValue, (int)change.NewValue);
    }

    private void OnCanvasSizeChanged(object? sender, SizeChangedEventArgs e) => Render();

    private void Render()
    {
        if (_typeColumnCanvas == null || _isRendering) return;
        if (_typeColumnCanvas.Bounds.Height == 0 || _typeColumnCanvas.Bounds.Width == 0) return;

        _isRendering = true;
        try
        {
            _typeColumnCanvas.Children.Clear();
            ColumnInfoList = null;
            _typeColValueRectMap = new();
            _typeColBorderRectMap = new();
            Maximum = Median = Total = string.Empty;

            var list = Data?.ToList();
            if (list == null || list.Count == 0) return;
            var firstData = list[0];
            if (firstData?.Values == null || firstData.Values.Length == 0) return;

            var colValueCount = firstData.Values.Length;
            var tempValueArr = new double[colValueCount];
            double maxValue = 0, total = 0;

            foreach (var item in list)
            {
                for (var i = 0; i < colValueCount; i++) tempValueArr[i] += item.Values[i];
                var max = item.Values.Max();
                if (max > maxValue) maxValue = max;
                total += item.Values.Sum();
            }

            if (DataMaximum > 0) maxValue = DataMaximum;
            else if (IsStack) maxValue = tempValueArr.Max();
            if (maxValue == 0) maxValue = 10;

            Maximum = CoverValue(maxValue);
            Median = CoverValue(maxValue / 2);
            Total = CoverValue(total);

            var columns = firstData.Values.Length;
            var margin = 5;
            if (columns <= 7) margin = 25;
            else if (columns <= 12) margin = 15;
            else if (columns >= 20) margin = 2;

            const double colNameHeight = 30;
            const double colNameBottomMargin = 5;
            var canvasHeight = _typeColumnCanvas.Bounds.Height - colNameHeight - colNameBottomMargin;
            var canvasWidth = _typeColumnCanvas.Bounds.Width;
            var columnBorderWidth = canvasWidth / columns;
            var colValueRectWidth = canvasWidth / columns - margin * 2;
            if (colValueRectWidth <= 0) colValueRectWidth = 1;
            if (canvasHeight <= 0) canvasHeight = 1;

            var columnNames = list[0].ColumnNames;
            var themeColor = Application.Current?.FindResource("ThemeColor");

            for (var i = 0; i < columns; i++)
            {
                var columnBorder = new Rectangle
                {
                    Width = columnBorderWidth,
                    Height = canvasHeight,
                    Fill = new SolidColorBrush(Colors.Transparent)
                };
                Canvas.SetLeft(columnBorder, i * columnBorderWidth);
                Canvas.SetTop(columnBorder, colNameBottomMargin);
                columnBorder.ZIndex = 999;
                _typeColumnCanvas.Children.Add(columnBorder);
                _typeColBorderRectMap[i] = columnBorder;

                var colName = columnNames != null && columnNames.Length > 0
                    ? columnNames[i]
                    : (i + NameIndexStart).ToString();
                var colNameText = new TextBlock
                {
                    TextAlignment = TextAlignment.Center,
                    Width = columnBorderWidth,
                    FontSize = 12,
                    Text = colName,
                    Foreground = Client.Base.Color.Colors.GetFromString("#FF8A8A8A")
                };
                Canvas.SetLeft(colNameText, i * columnBorderWidth);
                Canvas.SetBottom(colNameText, colNameBottomMargin);
                _typeColumnCanvas.Children.Add(colNameText);

                var valuesPopupList = new List<ChartColumnInfoModel>();
                _typeColValueRectMap[i] = new List<Rectangle>();

                foreach (var item in list)
                {
                    var colColor = item.Color ?? themeColor?.ToString() ?? StateData.ThemeColor;
                    var value = item.Values[i];
                    if (value > 0)
                    {
                        var colValueRect = new Rectangle
                        {
                            Width = colValueRectWidth,
                            Height = value / maxValue * canvasHeight,
                            Fill = Client.Base.Color.Colors.GetFromString(colColor)
                        };
                        if (!IsStack) { colValueRect.RadiusX = 4; colValueRect.RadiusY = 4; }
                        Canvas.SetLeft(colValueRect, i * columnBorderWidth + margin);
                        Canvas.SetBottom(colValueRect, colNameHeight);
                        _typeColumnCanvas.Children.Add(colValueRect);
                        _typeColValueRectMap[i].Add(colValueRect);

                        valuesPopupList.Add(new ChartColumnInfoModel
                        {
                            Color = item.Color,
                            Name = item.Name,
                            Icon = item.Icon,
                            Text = CoverValue(value) + Unit,
                            Value = value
                        });
                    }
                }

                var idx = i;
                columnBorder.PointerEntered += (_, _) =>
                {
                    var s = valuesPopupList;
                    if (s.Count > 1)
                        s.Add(new ChartColumnInfoModel { Name = "Sum", Text = CoverValue(s.Sum(x => x.Value)) + Unit, Color = "#FF8A8A8A" });
                    ColumnValuesInfoList = s;
                    ValuesPopupPlacementTarget = columnBorder;
                    IsShowValuesPopup = valuesPopupList.Count > 0;
                    ValuesPopupHorizontalOffset = 0;
                    if (ColumnSelectedIndex != idx)
                    {
                        var themeBrush = Application.Current?.Resources["ThemeBrush"] as SolidColorBrush;
                        columnBorder.Fill = new SolidColorBrush(themeBrush.Color) { Opacity = .05 };
                    }
                };
                columnBorder.PointerExited += (_, _) =>
                {
                    IsShowValuesPopup = false;
                    if (ColumnSelectedIndex != idx) columnBorder.Fill = new SolidColorBrush(Colors.Transparent);
                };
                if (IsCanColumnSelect)
                    columnBorder.PointerPressed += (_, _) => ColumnSelectedIndex = idx;
            }

            // z-index ordering
            foreach (var kvp in _typeColValueRectMap)
            {
                var rectList = IsStack ? kvp.Value : kvp.Value.OrderByDescending(m => m.Height).ToList();
                for (var i = 0; i < rectList.Count; i++)
                {
                    if (IsStack && i > 0)
                    {
                        var lastRect = rectList[i - 1];
                        Canvas.SetBottom(rectList[i], Canvas.GetBottom(lastRect) + lastRect.Height);
                    }
                    else if (!IsStack) { rectList[i].ZIndex = i; }
                }
            }

            // max/median/avg lines (all Y from top)
            DrawRefLine(_typeColumnCanvas, Maximum, "最大值", canvasWidth, colNameBottomMargin, Client.Base.Color.Colors.GetFromString("#ccc"));
            var midY = maxValue / 2 / maxValue * canvasHeight + colNameBottomMargin;
            DrawRefLine(_typeColumnCanvas, Median, "中间值", canvasWidth, midY, Client.Base.Color.Colors.GetFromString("#ccc"));
            var avg = tempValueArr.Average();
            var avgYFromTop = canvasHeight - (avg / maxValue * canvasHeight) + colNameBottomMargin;
            DrawRefLine(_typeColumnCanvas, CoverValue(avg), "平均值", canvasWidth, avgYFromTop, Client.Base.Color.Colors.GetFromString(StateData.ThemeColor));

            // legend
            var infoList = new List<ChartColumnInfoModel>();
            foreach (var item in list)
                infoList.Add(new ChartColumnInfoModel { Color = item.Color, Name = item.Name, Icon = item.Icon, Text = CoverValue(item.Values.Sum()) + Unit });
            ColumnInfoList = infoList;
        }
        finally { _isRendering = false; }
    }

    private void SetColBorderActiveBg(int oldIndex, int newIndex)
    {
        if (!IsCanColumnSelect || _typeColBorderRectMap == null) return;
        if (_typeColBorderRectMap.TryGetValue(oldIndex, out var oldItem))
            oldItem.Fill = new SolidColorBrush(Colors.Transparent);
        if (_typeColBorderRectMap.TryGetValue(newIndex, out var newItem))
        {
            var background = Application.Current?.Resources["ThemeBrush"] as SolidColorBrush;
            newItem.Fill = new SolidColorBrush(background.Color) { Opacity = .1 };
        }
    }

    private string CoverValue(double value) =>
        DataValueType == ChartDataValueType.Seconds ? Time.ToString((int)value) : value.ToString();

    private static void DrawRefLine(Canvas canvas, string text, string tooltip, double canvasWidth, double yFromTop, SolidColorBrush color)
    {
        var tb = new TextBlock { Text = text, FontSize = 12, Foreground = color };
        ToolTip.SetTip(tb, tooltip);
        var size = UIHelper.MeasureString(tb);
        tb.ZIndex = 1000;
        Canvas.SetRight(tb, 0);
        Canvas.SetTop(tb, yFromTop - size.Height / 2);
        canvas.Children.Add(tb);

        var line = new Line
        {
            Stroke = color,
            StrokeDashArray = [2, 5],
            StartPoint = new Point(5, yFromTop),
            EndPoint = new Point(canvasWidth - 5 - size.Width, yFromTop),
            StrokeThickness = 1
        };
        canvas.Children.Add(line);
    }
}

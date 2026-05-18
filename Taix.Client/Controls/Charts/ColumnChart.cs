using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
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

    private ColumnChartCanvas? _chartCanvas;
    private EmptyData? _emptyDataView;
    private Border? _chartBorder;

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
        _chartCanvas = e.NameScope.Find<ColumnChartCanvas>("TypeColumnCanvas");
        if (_chartCanvas != null)
            _chartCanvas.SetOwner(this);
        _emptyDataView = e.NameScope.Find<EmptyData>("EmptyDataView");
        _chartBorder = e.NameScope.Find<Border>("ChartBorder");
        UpdateVisibility();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DataProperty || change.Property == DataMaximumProperty ||
            change.Property == IsStackProperty || change.Property == NameIndexStartProperty)
        {
            UpdateVisibility();
            UpdateComputedValues();
            _chartCanvas?.InvalidateVisual();
        }

        if (change.Property == ColumnSelectedIndexProperty)
            _chartCanvas?.InvalidateVisual();
    }

    internal void UpdateComputedValues()
    {
        var list = Data?.ToList();
        if (list == null || list.Count == 0 || list[0]?.Values == null || list[0].Values.Length == 0)
        {
            Maximum = Median = Total = string.Empty;
            ColumnInfoList = null;
            return;
        }

        var colValueCount = list[0].Values.Length;
        var tempValueArr = new double[colValueCount];
        double maxValue = 0;

        foreach (var item in list)
        {
            for (var i = 0; i < colValueCount; i++) tempValueArr[i] += item.Values[i];
            var max = item.Values.Max();
            if (max > maxValue) maxValue = max;
        }

        if (DataMaximum > 0) maxValue = DataMaximum;
        else if (IsStack) maxValue = tempValueArr.Max();
        if (maxValue == 0) maxValue = 10;

        Maximum = CoverValue(maxValue);
        Median = CoverValue(maxValue / 2);
        Total = CoverValue(list.Sum(item => item.Values.Sum()));
        ColumnInfoList = BuildLegend(list, -1);
    }

    internal List<ChartColumnInfoModel> BuildLegend(List<ChartsDataModel> list, int colIndex)
    {
        var result = new List<ChartColumnInfoModel>();
        double total = 0;

        foreach (var item in list)
        {
            var value = colIndex < 0 ? item.Values.Sum() : item.Values[colIndex];
            if (value > 0)
            {
                result.Add(new ChartColumnInfoModel
                {
                    Color = item.Color,
                    Name = item.Name,
                    Icon = item.Icon,
                    Text = CoverValue(value) + Unit,
                    Value = value
                });
                total += value;
            }
        }

        result = result.OrderByDescending(x => x.Value).ToList();

        if (total > 0)
        {
            var totalName = Application.Current?.FindResource("Total") as string ?? "Total";
            result.Add(new ChartColumnInfoModel
            {
                Name = totalName,
                Text = CoverValue(total) + Unit,
                Color = "#FF8A8A8A"
            });
        }

        return result;
    }

    private void UpdateVisibility()
    {
        var list = Data?.ToList();
        var hasData = list != null && list.Count > 0 && list[0]?.Values != null && list[0].Values.Length > 0;
        if (_emptyDataView != null) _emptyDataView.IsVisible = !hasData;
        if (_chartBorder != null) _chartBorder.IsVisible = hasData;
    }

    internal string CoverValue(double value) =>
        DataValueType == ChartDataValueType.Seconds ? Time.ToString((int)value) : value.ToString();
}

public class ColumnChartCanvas : Control
{
    private ColumnChart? _owner;
    private int _hoverColumnIndex = -1;
    private double _columnWidth;
    private int _columnCount;
    private readonly Dictionary<int, List<ChartColumnInfoModel>> _columnPopupData = new();

    internal void SetOwner(ColumnChart owner) => _owner = owner;

    protected override Size MeasureOverride(Size availableSize) => availableSize;

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (_owner?.Data == null) return;

        var list = _owner.Data.ToList();
        if (list.Count == 0) return;

        var firstData = list[0];
        if (firstData?.Values == null || firstData.Values.Length == 0) return;

        var width = Bounds.Width;
        var height = Bounds.Height;
        if (width <= 0 || height <= 0) return;

        var colValueCount = firstData.Values.Length;
        var tempValueArr = new double[colValueCount];
        double maxValue = 0;

        foreach (var item in list)
        {
            for (var i = 0; i < colValueCount; i++) tempValueArr[i] += item.Values[i];
            var max = item.Values.Max();
            if (max > maxValue) maxValue = max;
        }

        var dataMaximum = _owner.DataMaximum;
        if (dataMaximum > 0) maxValue = dataMaximum;
        else if (_owner.IsStack) maxValue = tempValueArr.Max();
        if (maxValue == 0) maxValue = 10;

        var columns = firstData.Values.Length;
        _columnCount = columns;
        var margin = 5;
        if (columns <= 7) margin = 25;
        else if (columns <= 12) margin = 15;
        else if (columns >= 20) margin = 2;

        const double colNameHeight = 30;
        const double colNameBottomMargin = 5;
        var chartHeight = height - colNameHeight - colNameBottomMargin;
        _columnWidth = width / columns;
        var colValueRectWidth = width / columns - margin * 2;
        if (colValueRectWidth <= 0) colValueRectWidth = 1;
        if (chartHeight <= 0) chartHeight = 1;

        var columnNames = firstData.ColumnNames;
        var themeColorStr = Application.Current?.FindResource("ThemeColor")?.ToString() ?? StateData.ThemeColor;
        var subTextBrush = new SolidColorBrush(Color.Parse("#FF8A8A8A"));

        _columnPopupData.Clear();

        // 绘制列名
        for (var i = 0; i < columns; i++)
        {
            var colName = columnNames != null && columnNames.Length > 0
                ? columnNames[i]
                : (i + _owner.NameIndexStart).ToString();

            var formattedText = CreateFormattedText(colName, 12, subTextBrush);
            var textX = i * _columnWidth + (_columnWidth - formattedText.Width) / 2;
            var textY = height - colNameBottomMargin - formattedText.Height;
            context.DrawText(formattedText, new Point(textX, textY));
        }

        // 绘制柱子
        for (var i = 0; i < columns; i++)
        {
            var valuesPopupList = new List<ChartColumnInfoModel>();
            double currentBottom = colNameHeight;

            foreach (var item in list)
            {
                var colColorStr = item.Color ?? themeColorStr;
                var value = item.Values[i];
                if (value > 0)
                {
                    var rectHeight = value / maxValue * chartHeight;
                    var rectX = i * _columnWidth + margin;
                    var rectY = height - currentBottom - rectHeight;

                    var color = Color.Parse(colColorStr);
                    var brush = new SolidColorBrush(color);
                    var radius = !_owner.IsStack ? 4 : 0;

                    context.DrawRectangle(brush, null, new Rect(rectX, rectY, colValueRectWidth, rectHeight), radius, radius);

                    valuesPopupList.Add(new ChartColumnInfoModel
                    {
                        Color = item.Color,
                        Name = item.Name,
                        Icon = item.Icon,
                        Text = _owner.CoverValue(value) + _owner.Unit,
                        Value = value
                    });

                    if (_owner.IsStack)
                        currentBottom += rectHeight;
                }
            }

            _columnPopupData[i] = valuesPopupList;
        }

        // 绘制参考线
        var grayBrush = new SolidColorBrush(Color.Parse("#FF8A8A8A"));
        var themeColor = Color.Parse(themeColorStr);

        DrawRefLine(context, _owner.Maximum, chartHeight, colNameBottomMargin, width, grayBrush);
        var midY = chartHeight / 2 + colNameBottomMargin;
        DrawRefLine(context, _owner.Median, chartHeight, midY, width, grayBrush);
        var avg = tempValueArr.Average();
        var avgText = _owner.CoverValue(avg);
        var avgY = chartHeight - (avg / maxValue * chartHeight) + colNameBottomMargin;
        DrawRefLine(context, avgText, chartHeight, avgY, width, new SolidColorBrush(themeColor));

        // 绘制 hover 高亮
        DrawHighlight(context, _hoverColumnIndex, chartHeight, colNameBottomMargin, themeColor, 0.05);
        // 绘制选中高亮
        if (_owner.IsCanColumnSelect)
            DrawHighlight(context, _owner.ColumnSelectedIndex, chartHeight, colNameBottomMargin, themeColor, 0.1);
    }

    private void DrawHighlight(DrawingContext context, int colIndex, double chartHeight, double topMargin, Color themeColor, double opacity)
    {
        if (colIndex < 0 || colIndex >= _columnCount) return;
        var rect = new Rect(colIndex * _columnWidth, topMargin, _columnWidth, chartHeight);
        context.FillRectangle(new SolidColorBrush(themeColor) { Opacity = opacity }, rect);
    }

    private void DrawRefLine(DrawingContext context, string text, double chartHeight, double yFromTop, double canvasWidth, IBrush color)
    {
        if (string.IsNullOrEmpty(text)) return;

        var formattedText = CreateFormattedText(text, 12, color);
        var textX = canvasWidth - formattedText.Width;
        var textY = yFromTop - formattedText.Height / 2;

        if (textY < 0) textY = 0;
        if (textY + formattedText.Height > chartHeight + 5) textY = chartHeight + 5 - formattedText.Height;

        context.DrawText(formattedText, new Point(textX, textY));

        var dashPen = new Pen(color, 1.0, new DashStyle(new[] { 2.0, 5.0 }, 0));
        context.DrawLine(dashPen, new Point(5, yFromTop), new Point(canvasWidth - 5 - formattedText.Width, yFromTop));
    }

    private static FormattedText CreateFormattedText(string text, double size, IBrush foreground)
    {
        return new FormattedText(
            text,
            SystemLanguage.CurrentCultureInfo ?? CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(FontFamily.Default),
            size,
            foreground);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_owner == null) return;

        var pos = e.GetPosition(this);
        var colIndex = (int)(pos.X / _columnWidth);

        if (colIndex < 0 || colIndex >= _columnCount)
        {
            if (_hoverColumnIndex >= 0)
            {
                ClearHover();
                if (_owner.ColumnSelectedIndex < 0)
                    RestoreDefaultLegend();
            }
            return;
        }

        if (colIndex != _hoverColumnIndex)
        {
            ClearHover();
            _hoverColumnIndex = colIndex;
            InvalidateVisual();

            if (_owner.ColumnSelectedIndex < 0)
                UpdateLegendForColumn(colIndex);

            if (_columnPopupData.TryGetValue(colIndex, out var valuesPopupList) && valuesPopupList.Count > 0)
            {
                var popupList = new List<ChartColumnInfoModel>(valuesPopupList);
                var total = popupList.Sum(x => x.Value);
                if (total > 0)
                {
                    var totalName = Application.Current?.FindResource("Total") as string ?? "Total";
                    popupList.Add(new ChartColumnInfoModel
                    {
                        Name = totalName,
                        Text = _owner.CoverValue(total) + _owner.Unit,
                        Color = "#FF8A8A8A"
                    });
                }
                _owner.ColumnValuesInfoList = popupList;
                _owner.ValuesPopupPlacementTarget = this;
                _owner.IsShowValuesPopup = true;

                var colCenterX = colIndex * _columnWidth + _columnWidth / 2;
                var canvasCenterX = Bounds.Width / 2;
                _owner.ValuesPopupHorizontalOffset = colCenterX - canvasCenterX;
            }
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        ClearHover();
        _owner.IsShowValuesPopup = false;
        if (_owner?.ColumnSelectedIndex < 0)
            RestoreDefaultLegend();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (_owner == null) return;

        var pos = e.GetPosition(this);
        var colIndex = (int)(pos.X / _columnWidth);

        if (colIndex >= 0 && colIndex < _columnCount && _owner.IsCanColumnSelect)
        {
            if (_owner.ColumnSelectedIndex == colIndex)
            {
                _owner.ColumnSelectedIndex = -1;
                RestoreDefaultLegend();
            }
            else
            {
                _owner.ColumnSelectedIndex = colIndex;
                UpdateLegendForColumn(colIndex);
            }
        }
        else if ((colIndex < 0 || colIndex >= _columnCount) && _owner.IsCanColumnSelect)
        {
            _owner.ColumnSelectedIndex = -1;
            RestoreDefaultLegend();
        }
    }

    private void UpdateLegendForColumn(int colIndex)
    {
        if (_owner == null) return;
        var list = _owner.Data?.ToList();
        if (list == null || list.Count == 0) return;
        _owner.ColumnInfoList = _owner.BuildLegend(list, colIndex);
    }

    private void RestoreDefaultLegend()
    {
        _owner?.UpdateComputedValues();
    }

    private void ClearHover()
    {
        if (_hoverColumnIndex >= 0)
        {
            _hoverColumnIndex = -1;
            InvalidateVisual();
        }
    }
}

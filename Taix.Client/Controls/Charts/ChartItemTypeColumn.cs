using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Taix.Client.Base.Color;

namespace Taix.Client.Controls.Charts;

public class ChartItemTypeColumn : TemplatedControl
{
    public static readonly StyledProperty<double> MaxValueProperty =
        AvaloniaProperty.Register<ChartItemTypeColumn, double>(nameof(MaxValue));

    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<ChartItemTypeColumn, double>(nameof(Value));

    public static readonly StyledProperty<string> ColorProperty =
        AvaloniaProperty.Register<ChartItemTypeColumn, string>(nameof(Color));

    public static readonly StyledProperty<string> ColumnNameProperty =
        AvaloniaProperty.Register<ChartItemTypeColumn, string>(nameof(ColumnName));


    private readonly bool _isRendering = false;
    private Rectangle _valueBlockObj;
    private Border _valueContainer;

    public ChartItemTypeColumn()
    {
        Unloaded += ChartItemTypeColumn_Unloaded;
    }

    public double MaxValue
    {
        get => GetValue(MaxValueProperty);
        set => SetValue(MaxValueProperty, value);
    }

    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public string Color
    {
        get => GetValue(ColorProperty);
        set => SetValue(ColorProperty, value);
    }

    public string ColumnName
    {
        get => GetValue(ColumnNameProperty);
        set => SetValue(ColumnNameProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(ChartItemTypeColumn);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _valueBlockObj = e.NameScope.Get<Rectangle>("ValueBlockObj");
        _valueContainer = e.NameScope.Get<Border>("ValueContainer");
        Loaded -= ChartItemTypeColumn_Loaded;
        Loaded += ChartItemTypeColumn_Loaded;
    }

    private void ChartItemTypeColumn_Unloaded(object? sender, RoutedEventArgs e)
    {
        Loaded -= ChartItemTypeColumn_Loaded;
        Unloaded -= ChartItemTypeColumn_Unloaded;
    }

    private void ChartItemTypeColumn_Loaded(object? sender, RoutedEventArgs e)
    {
        Render();
    }

    private void Render()
    {
        if (_isRendering) return;

        if (!string.IsNullOrEmpty(Color)) _valueBlockObj.Fill = Colors.GetFromString(Color);
        Update();
    }

    public void Update()
    {
        _valueBlockObj.Height = Value / MaxValue * _valueContainer.Bounds.Height;
    }
}

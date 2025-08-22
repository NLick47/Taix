using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using UI.Base.Color;

namespace UI.Controls.Charts;

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


    private readonly bool isRendering = false;
    private bool IsAddEvent;
    private Rectangle ValueBlockObj;
    private Border ValueContainer;

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
        ValueBlockObj = e.NameScope.Get<Rectangle>("ValueBlockObj");
        ValueContainer = e.NameScope.Get<Border>("ValueContainer");
        if (!IsAddEvent) Loaded += ChartItemTypeColumn_Loaded;
    }

    private void ChartItemTypeColumn_Unloaded(object sender, RoutedEventArgs e)
    {
        Loaded -= ChartItemTypeColumn_Loaded;
        Unloaded -= ChartItemTypeColumn_Unloaded;
    }

    private void ChartItemTypeColumn_Loaded(object sender, RoutedEventArgs e)
    {
        Render();
        IsAddEvent = true;
    }

    private void Render()
    {
        if (isRendering) return;

        if (!string.IsNullOrEmpty(Color)) ValueBlockObj.Fill = Colors.GetFromString(Color);
        Update();
    }

    public void Update()
    {
        ValueBlockObj.Height = Value / MaxValue * ValueContainer.Bounds.Height;
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using UI.Controls.Charts.Model;
using Colors = UI.Base.Color.Colors;

namespace UI.Controls.Charts;

public class ChartsItemTypePie : Canvas
{
    public static readonly DirectProperty<ChartsItemTypePie, List<ChartsDataModel>?> DataProperty =
        AvaloniaProperty.RegisterDirect<ChartsItemTypePie, List<ChartsDataModel>?>(
            nameof(Data),
            o => o.Data,
            (o, v) => o.Data = v);

 
    public static readonly DirectProperty<ChartsItemTypePie, double> MaxValueProperty =
        AvaloniaProperty.RegisterDirect<ChartsItemTypePie, double>(
            nameof(MaxValue),
            o => o.MaxValue,
            (o, v) => o.MaxValue = v);


    public static readonly StyledProperty<double> InnerRadiusProperty =
        AvaloniaProperty.Register<ChartsItemTypePie, double>(nameof(InnerRadius), 60);


    public static readonly StyledProperty<double> OuterRadiusProperty =
        AvaloniaProperty.Register<ChartsItemTypePie, double>(nameof(OuterRadius), 80);

    private readonly List<Path> _paths = new();
    private List<ChartsDataModel>? _data = new();
    private double _lastAngle = -Math.PI / 2;
    private double _maxValue;
  

    public List<ChartsDataModel>? Data
    {
        get => _data;
        set => SetAndRaise(DataProperty, ref _data, value);
    }

    public double MaxValue
    {
        get => _maxValue;
        set => SetAndRaise(MaxValueProperty, ref _maxValue, value);
    }

    public double InnerRadius
    {
        get => GetValue(InnerRadiusProperty);
        set => SetValue(InnerRadiusProperty, value);
    }

    public double OuterRadius
    {
        get => GetValue(OuterRadiusProperty);
        set => SetValue(OuterRadiusProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(ChartsItemTypePie);

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        Render();
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        Render();
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        foreach (var item in _paths)
        {
            item.PointerEntered -= Path_PointerEntered;
            item.PointerExited -= Path_PointerExited;
        }

        foreach (var item in Children)
        {
            item.PointerEntered -= Path_PointerEntered;
            item.PointerExited -= Path_PointerExited;
        }
    }

    private void Render()
    {
        _paths.Clear();
        Children.Clear();
        
        if (Data == null || Data.Count == 0)
            return;
        
        if (Bounds.Width > 0 && Bounds.Height > 0)
        {
            double size = Math.Min(Bounds.Width, Bounds.Height);
            if (GetValue(OuterRadiusProperty) == 80)
                OuterRadius = size * 0.35;
            if (GetValue(InnerRadiusProperty) == 60)
                InnerRadius = size * 0.25;
        }
        
        MaxValue = Data.Sum(m => m.Value);
        _lastAngle = -Math.PI / 2;

        foreach (var item in Data)
        {
            var angle = item.Value / MaxValue * 360;
            var path = CreatePath(angle, Colors.GetFromString(item.Color));
            path.PointerEntered += Path_PointerEntered;
            path.PointerExited += Path_PointerExited;
            _paths.Add(path);
            Children.Add(path);
        }
    }

    private void Path_PointerExited(object? sender, PointerEventArgs e)
    {
        foreach (var p in _paths) 
            p.Opacity = 1;
    }

    private void Path_PointerEntered(object? sender, PointerEventArgs e)
    {
        var path = sender as Path;
        foreach (var p in _paths)
            if (p != path)
                p.Opacity = .2;
    }

    private Path CreatePath(double angle, SolidColorBrush color)
    {
        var path = new Path();
        var pathGeometry = new PathGeometry();

        double centerX = Bounds.Width / 2;
        double centerY = Bounds.Height / 2;
        
        double innerRadius = InnerRadius;
        double outerRadius = OuterRadius;
        
        double startAngle = _lastAngle;
        double sweepAngle = angle * Math.PI / 180;
        double endAngle = startAngle + sweepAngle;
        
        double startInnerX = centerX + innerRadius * Math.Cos(startAngle);
        double startInnerY = centerY + innerRadius * Math.Sin(startAngle);
        double startOuterX = centerX + outerRadius * Math.Cos(startAngle);
        double startOuterY = centerY + outerRadius * Math.Sin(startAngle);
        
        double endOuterX = centerX + outerRadius * Math.Cos(endAngle);
        double endOuterY = centerY + outerRadius * Math.Sin(endAngle);
        double endInnerX = centerX + innerRadius * Math.Cos(endAngle);
        double endInnerY = centerY + innerRadius * Math.Sin(endAngle);
        
        var fig = new PathFigure
        {
            StartPoint = new Point(startInnerX, startInnerY),
            Segments = new PathSegments
            {
                new LineSegment(){Point = new Point(startOuterX, startOuterY)},
                new ArcSegment
                {
                    Point = new Point(endOuterX, endOuterY),
                    Size = new Size(outerRadius, outerRadius),
                    SweepDirection = SweepDirection.Clockwise,
                    IsLargeArc = angle > 180
                },
                new LineSegment(){Point = new Point(endInnerX, endInnerY)},
                new ArcSegment
                {
                    Point = new Point(startInnerX, startInnerY),
                    Size = new Size(innerRadius, innerRadius),
                    SweepDirection = SweepDirection.CounterClockwise,
                    IsLargeArc = angle > 180
                }
            },
            IsClosed = true
        };
        
        pathGeometry.Figures!.Add(fig);
        path.Data = pathGeometry;
        path.Fill = color;
        path.Stroke = Brushes.Transparent; 
        
        _lastAngle = endAngle;
        
        return path;
    }
}
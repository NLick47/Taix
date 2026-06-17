using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Taix.Client.Controls.Charts.Model;

namespace Taix.Client.Controls.Charts;

public class ChartsItemTypePie : Control
{
    public static readonly DirectProperty<ChartsItemTypePie, List<ChartsDataModel>?> DataProperty =
        AvaloniaProperty.RegisterDirect<ChartsItemTypePie, List<ChartsDataModel>?>(
            nameof(Data), o => o.Data, (o, v) => o.Data = v);

    public static readonly StyledProperty<double> InnerRadiusRatioProperty =
        AvaloniaProperty.Register<ChartsItemTypePie, double>(nameof(InnerRadiusRatio), 0.6);

    private List<ChartsDataModel>? _data = [];
    private int _hoveredIndex = -1;
    private bool _isDarkTheme;

    public List<ChartsDataModel>? Data
    {
        get => _data;
        set => SetAndRaise(DataProperty, ref _data, value);
    }

    public double InnerRadiusRatio
    {
        get => GetValue(InnerRadiusRatioProperty);
        set => SetValue(InnerRadiusRatioProperty, value);
    }

    public ChartsItemTypePie()
    {
        AffectsRender<ChartsItemTypePie>(DataProperty, InnerRadiusRatioProperty);
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        _isDarkTheme = Application.Current?.ActualThemeVariant == ThemeVariant.Dark;

        if (Application.Current != null)
        {
            Application.Current.ActualThemeVariantChanged += OnThemeChanged;
        }
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        if (Application.Current != null)
        {
            Application.Current.ActualThemeVariantChanged -= OnThemeChanged;
        }
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        _isDarkTheme = Application.Current?.ActualThemeVariant == ThemeVariant.Dark;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var width = Bounds.Width;
        var height = Bounds.Height;

        if (width <= 0 || height <= 0) return;

        if (Data == null || Data.Count == 0)
        {
            DrawEmptyState(context, width, height);
            return;
        }

        var totalValue = Data.Sum(m => m.Value);
        if (totalValue <= 0)
        {
            DrawEmptyState(context, width, height);
            return;
        }

        var outerRadius = Math.Min(width, height) * 0.38;
        var innerRadius = outerRadius * Math.Clamp(InnerRadiusRatio, 0, 0.95);
        var center = new Point(width / 2, height / 2);

        // 绘制扇形
        var currentAngle = -Math.PI / 2;
        for (var i = 0; i < Data.Count; i++)
        {
            var item = Data[i];
            if (item.Value <= 0) continue;

            var sweepAngle = item.Value / totalValue * 2 * Math.PI;
            var isHovered = _hoveredIndex == i;

            DrawSlice(context, center, innerRadius, outerRadius, currentAngle, sweepAngle, item.Color, isHovered);

            currentAngle += sweepAngle;
        }

        DrawCenterCircle(context, center, innerRadius);

        if (_hoveredIndex >= 0 && Data != null && _hoveredIndex < Data.Count)
        {
            DrawTooltip(context, center, innerRadius, outerRadius, Data[_hoveredIndex], totalValue);
        }
    }

    private void DrawEmptyState(DrawingContext context, double width, double height)
    {
        var center = new Point(width / 2, height / 2);
        var radius = Math.Min(width, height) * 0.15;

        var bgColor = _isDarkTheme ? Color.Parse("#3A3A42") : Color.Parse("#E8E8EC");
        context.DrawEllipse(new SolidColorBrush(bgColor), null, center, radius, radius);
    }

    private void DrawSlice(DrawingContext context, Point center, double innerRadius, double outerRadius, double startAngle, double sweepAngle, string colorString, bool isHovered)
    {
        var baseColor = TryParseColor(colorString);
        var fillColor = isHovered ? LightenColor(baseColor, 0.15f) : baseColor;
        var effectiveOuterRadius = isHovered ? outerRadius + 4 : outerRadius;
        var effectiveInnerRadius = isHovered ? innerRadius - 2 : innerRadius;

        var geometry = CreateSliceGeometry(center, effectiveInnerRadius, effectiveOuterRadius, startAngle, sweepAngle);

        context.DrawGeometry(new SolidColorBrush(fillColor), null, geometry);
    }

    private StreamGeometry CreateSliceGeometry(Point center, double innerRadius, double outerRadius, double startAngle, double sweepAngle)
    {
        var geometry = new StreamGeometry();

        using (var ctx = geometry.Open())
        {
            const double fullCircle = 2 * Math.PI;

            if (sweepAngle >= fullCircle - 0.0001)
            {
                var epsilon = 0.0001;
                var actualSweep = fullCircle - epsilon;

                var startRad = startAngle;
                var endRad = startAngle + actualSweep;

                var startInner = new Point(center.X + innerRadius * Math.Cos(startRad), center.Y + innerRadius * Math.Sin(startRad));
                var startOuter = new Point(center.X + outerRadius * Math.Cos(startRad), center.Y + outerRadius * Math.Sin(startRad));
                var endOuter = new Point(center.X + outerRadius * Math.Cos(endRad), center.Y + outerRadius * Math.Sin(endRad));
                var endInner = new Point(center.X + innerRadius * Math.Cos(endRad), center.Y + innerRadius * Math.Sin(endRad));

                ctx.BeginFigure(startInner, true);
                ctx.LineTo(startOuter);
                ctx.ArcTo(endOuter, new Size(outerRadius, outerRadius), 0, true, SweepDirection.Clockwise);
                ctx.LineTo(endInner);
                if (innerRadius > 0)
                {
                    ctx.ArcTo(startInner, new Size(innerRadius, innerRadius), 0, true, SweepDirection.CounterClockwise);
                }
                ctx.EndFigure(true);
            }
            else
            {
                var startRad = startAngle;
                var endRad = startAngle + sweepAngle;

                var startInner = new Point(center.X + innerRadius * Math.Cos(startRad), center.Y + innerRadius * Math.Sin(startRad));
                var startOuter = new Point(center.X + outerRadius * Math.Cos(startRad), center.Y + outerRadius * Math.Sin(startRad));
                var endOuter = new Point(center.X + outerRadius * Math.Cos(endRad), center.Y + outerRadius * Math.Sin(endRad));
                var endInner = new Point(center.X + innerRadius * Math.Cos(endRad), center.Y + innerRadius * Math.Sin(endRad));

                var angleDegrees = sweepAngle * 180 / Math.PI;

                ctx.BeginFigure(startInner, true);
                ctx.LineTo(startOuter);
                ctx.ArcTo(endOuter, new Size(outerRadius, outerRadius), 0, angleDegrees > 180, SweepDirection.Clockwise);
                ctx.LineTo(endInner);
                if (innerRadius > 0)
                {
                    ctx.ArcTo(startInner, new Size(innerRadius, innerRadius), 0, angleDegrees > 180, SweepDirection.CounterClockwise);
                }
                ctx.EndFigure(true);
            }
        }

        return geometry;
    }

    private void DrawCenterCircle(DrawingContext context, Point center, double innerRadius)
    {
        var borderColor = _isDarkTheme ? Color.Parse("#3A3A42") : Color.Parse("#E8E8EC");
        context.DrawEllipse(null, new Pen(new SolidColorBrush(borderColor), 1), center, innerRadius, innerRadius);
    }

    private void DrawTooltip(DrawingContext context, Point center, double innerRadius, double outerRadius, ChartsDataModel item, double totalValue)
    {
        var percentage = item.Value / totalValue * 100;
        var nameText = item.Name;
        var percentText = $"{percentage:F1}%";

        var bgColor = _isDarkTheme ? Color.Parse("#404048") : Color.Parse("#FFFFFF");
        var nameColor = _isDarkTheme ? Color.Parse("#A0A0A8") : Color.Parse("#888888");
        var percentColor = _isDarkTheme ? Color.Parse("#F0F0F0") : Color.Parse("#333333");

        var typeface = new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.Medium);
        var boldTypeface = new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold);

        var nameFormatted = new FormattedText(nameText, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, 12, new SolidColorBrush(nameColor));
        var percentFormatted = new FormattedText(percentText, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, boldTypeface, 16, new SolidColorBrush(percentColor));

        var maxWidth = Math.Max(nameFormatted.Width, percentFormatted.Width);
        var totalHeight = nameFormatted.Height + percentFormatted.Height + 4;
        var padding = 10;

        var tooltipWidth = maxWidth + padding * 2;
        var tooltipHeight = totalHeight + padding * 2 - 4;

        // 居中显示
        var tooltipX = center.X - tooltipWidth / 2;
        var tooltipY = center.Y - tooltipHeight / 2;

        // 绘制背景
        var tooltipRect = new Rect(tooltipX, tooltipY, tooltipWidth, tooltipHeight);
        context.DrawRectangle(new SolidColorBrush(bgColor), null, tooltipRect, 6, 6);

        // 绘制文字
        var nameX = center.X - nameFormatted.Width / 2;
        var nameY = tooltipY + padding - 2;
        context.DrawText(nameFormatted, new Point(nameX, nameY));

        var percentX = center.X - percentFormatted.Width / 2;
        var percentY = nameY + nameFormatted.Height + 2;
        context.DrawText(percentFormatted, new Point(percentX, percentY));
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        var position = e.GetPosition(this);
        var newHoveredIndex = GetSliceAtPosition(position);

        if (newHoveredIndex != _hoveredIndex)
        {
            _hoveredIndex = newHoveredIndex;
            InvalidateVisual();
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (_hoveredIndex != -1)
        {
            _hoveredIndex = -1;
            InvalidateVisual();
        }
    }

    private int GetSliceAtPosition(Point position)
    {
        if (Data == null || Data.Count == 0) return -1;

        var totalValue = Data.Sum(d => d.Value);
        if (totalValue <= 0) return -1;

        var width = Bounds.Width;
        var height = Bounds.Height;
        var center = new Point(width / 2, height / 2);
        var outerRadius = Math.Min(width, height) * 0.38;
        var innerRadius = outerRadius * InnerRadiusRatio;

        var dx = position.X - center.X;
        var dy = position.Y - center.Y;
        var distance = Math.Sqrt(dx * dx + dy * dy);

        if (distance < innerRadius || distance > outerRadius + 5) return -1;

        var angle = Math.Atan2(dy, dx) * 180 / Math.PI;
        angle = (angle + 90 + 360) % 360;

        var currentAngle = 0.0;
        for (var i = 0; i < Data.Count; i++)
        {
            if (Data[i].Value <= 0) continue;

            var sweepAngle = Data[i].Value / totalValue * 360;
            if (angle >= currentAngle && angle < currentAngle + sweepAngle)
                return i;

            currentAngle += sweepAngle;
        }

        return -1;
    }

    private static Color LightenColor(Color color, float amount)
    {
        var r = (byte)Math.Min(255, color.R + (255 - color.R) * amount);
        var g = (byte)Math.Min(255, color.G + (255 - color.G) * amount);
        var b = (byte)Math.Min(255, color.B + (255 - color.B) * amount);
        return Color.FromRgb(r, g, b);
    }

    private static Color TryParseColor(string? colorString)
    {
        if (string.IsNullOrEmpty(colorString)) return Color.Parse("#78909C");
        try { return Color.Parse(colorString); }
        catch { return Color.Parse("#78909C"); }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var w = double.IsInfinity(availableSize.Width) ? 200 : availableSize.Width;
        var h = double.IsInfinity(availableSize.Height) ? 200 : availableSize.Height;
        return new Size(w, h);
    }
}

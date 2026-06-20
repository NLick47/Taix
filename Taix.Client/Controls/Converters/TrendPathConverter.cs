using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Taix.Client.Shared.Models.Category;

namespace Taix.Client.Controls.Converters;

public class TrendPathConverter : IValueConverter
{
    public static readonly TrendPathConverter AreaInstance = new() { Closed = true };
    public static readonly TrendPathConverter LineInstance = new() { Closed = false };

    public bool Closed { get; init; } = true;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not IEnumerable enumerable) return null;

        var points = new List<long>();
        foreach (var item in enumerable)
        {
            if (item is DailyPointModel p) points.Add(p.Seconds);
        }
        if (points.Count == 0) return null;

        var (w, h) = ParseSize(parameter);
        if (points.Count == 1) points.Add(points[0]);

        var max = (double)points.Max();
        if (max <= 0) max = 1;

        var n = points.Count;
        var dx = w / (n - 1);
        var pts = new List<Point>(n);
        for (var i = 0; i < n; i++)
        {
            var x = i * dx;
            var y = h - (points[i] / max) * (h - 4);
            pts.Add(new Point(x, y));
        }

        // Catmull-Rom 转贝塞尔
        var sb = new System.Text.StringBuilder();
        sb.Append('M').Append(pts[0].X.ToString("F2", culture)).Append(',').Append(pts[0].Y.ToString("F2", culture));

        for (var i = 0; i < pts.Count - 1; i++)
        {
            var p0 = i == 0 ? pts[0] : pts[i - 1];
            var p1 = pts[i];
            var p2 = pts[i + 1];
            var p3 = i + 2 < pts.Count ? pts[i + 2] : p2;

            // tension 0.5 = 标准 Catmull-Rom
            var c1 = new Point(p1.X + (p2.X - p0.X) / 6, p1.Y + (p2.Y - p0.Y) / 6);
            var c2 = new Point(p2.X - (p3.X - p1.X) / 6, p2.Y - (p3.Y - p1.Y) / 6);

            sb.Append(" C")
              .Append(c1.X.ToString("F2", culture)).Append(',').Append(c1.Y.ToString("F2", culture)).Append(' ')
              .Append(c2.X.ToString("F2", culture)).Append(',').Append(c2.Y.ToString("F2", culture)).Append(' ')
              .Append(p2.X.ToString("F2", culture)).Append(',').Append(p2.Y.ToString("F2", culture));
        }

        if (Closed)
        {
            sb.Append(" L").Append((w).ToString("F2", culture)).Append(',').Append(h.ToString("F2", culture));
            sb.Append(" L").Append("0,").Append(h.ToString("F2", culture));
            sb.Append(" Z");
        }

        try
        {
            return Geometry.Parse(sb.ToString());
        }
        catch
        {
            return null;
        }
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();

    private static (double w, double h) ParseSize(object? parameter)
    {
        if (parameter is string s)
        {
            var parts = s.Split(',', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2 &&
                double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var w) &&
                double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var h))
            {
                return (w, h);
            }
        }
        return (1000, 200);
    }
}

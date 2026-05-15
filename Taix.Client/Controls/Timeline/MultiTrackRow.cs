using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace Taix.Client.Controls.Timeline;

public class MultiTrackRow : Control
{
    private const int SecondsPerHour = 3600;
    private const double MinSegmentWidth = 3;
    private static readonly Color DefaultColor = Color.Parse("#888888");
    private static readonly Color HighlightOverlay = Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF);
    private static readonly Color ShadowOverlay = Color.FromArgb(0x0A, 0x00, 0x00, 0x00);

    public static readonly StyledProperty<IEnumerable<MultiTrackSegment>> SegmentsProperty =
        AvaloniaProperty.Register<MultiTrackRow, IEnumerable<MultiTrackSegment>>(nameof(Segments));

    public static readonly StyledProperty<double> VisibleStartHourProperty =
        AvaloniaProperty.Register<MultiTrackRow, double>(nameof(VisibleStartHour), 0.0);

    public static readonly StyledProperty<double> VisibleEndHourProperty =
        AvaloniaProperty.Register<MultiTrackRow, double>(nameof(VisibleEndHour), 24.0);

    public IEnumerable<MultiTrackSegment> Segments
    {
        get => GetValue(SegmentsProperty);
        set => SetValue(SegmentsProperty, value);
    }

    public double VisibleStartHour
    {
        get => GetValue(VisibleStartHourProperty);
        set => SetValue(VisibleStartHourProperty, value);
    }

    public double VisibleEndHour
    {
        get => GetValue(VisibleEndHourProperty);
        set => SetValue(VisibleEndHourProperty, value);
    }

    // Tooltip state
    private Point? _mousePos;
    private (MultiTrackSegment Seg, Rect Bounds)? _hoveredHit;
    private List<(MultiTrackSegment Seg, Rect Bounds)> _segmentRects = new();

    // Tooltip brushes
    private IBrush _tipBg = null!;
    private IPen _tipPen = null!;
    private IBrush _tipText = null!;
    private IBrush _tipShadow = null!;

    // Period background brushes
    private IBrush _periodNightBrush = null!;
    private IBrush _periodMorningBrush = null!;
    private IBrush _periodNoonBrush = null!;
    private IBrush _periodAfternoonBrush = null!;
    private IBrush _periodEveningBrush = null!;

    public MultiTrackRow()
    {
        ClipToBounds = true;
        LoadBrushes();
    }

    private void LoadBrushes()
    {
        _tipBg = Application.Current?.FindResource("TimelineTipBgBrush") as IBrush
                 ?? new SolidColorBrush(Color.Parse("#161b22"));
        _tipPen = new Pen(
            Application.Current?.FindResource("TimelineTipBorderBrush") as IBrush
            ?? new SolidColorBrush(Color.Parse("#30363d")), 1);
        _tipText = Application.Current?.FindResource("TimelineTipTextBrush") as IBrush
                   ?? new SolidColorBrush(Colors.White);
        _tipShadow = Application.Current?.FindResource("TimelineTooltipShadowBrush") as IBrush
                     ?? new SolidColorBrush(Color.FromArgb(0x80, 0, 0, 0));
        _periodNightBrush = Application.Current?.FindResource("TimelinePeriodNightBgBrush") as IBrush
                            ?? new SolidColorBrush(Color.Parse("#14182e"));
        _periodMorningBrush = Application.Current?.FindResource("TimelinePeriodMorningBgBrush") as IBrush
                              ?? new SolidColorBrush(Color.Parse("#142014"));
        _periodNoonBrush = Application.Current?.FindResource("TimelinePeriodNoonBgBrush") as IBrush
                           ?? new SolidColorBrush(Color.Parse("#1c1c24"));
        _periodAfternoonBrush = Application.Current?.FindResource("TimelinePeriodAfternoonBgBrush") as IBrush
                                ?? new SolidColorBrush(Color.Parse("#241c14"));
        _periodEveningBrush = Application.Current?.FindResource("TimelinePeriodEveningBgBrush") as IBrush
                              ?? new SolidColorBrush(Color.Parse("#1c1424"));
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SegmentsProperty
            || change.Property == VisibleStartHourProperty
            || change.Property == VisibleEndHourProperty)
        {
            InvalidateVisual();
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        var pos = e.GetPosition(this);
        var prevHovered = _hoveredHit;
        UpdateHoveredHit(pos);

        // hover segment 变化时重绘
        if (_hoveredHit?.Seg != prevHovered?.Seg)
        {
            InvalidateVisual();
        }

        _mousePos = pos;
    }

    private void UpdateHoveredHit(Point pos)
    {
        _hoveredHit = _segmentRects.FirstOrDefault(s => s.Bounds.Contains(pos));
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        _mousePos = null;
        _hoveredHit = null;
        InvalidateVisual();
    }

    public override void Render(DrawingContext ctx)
    {
        var bounds = Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        var viewStartSec = Math.Min(VisibleStartHour, VisibleEndHour) * SecondsPerHour;
        var viewEndSec = Math.Max(VisibleStartHour, VisibleEndHour) * SecondsPerHour;
        var viewDuration = viewEndSec - viewStartSec;
        if (viewDuration <= 0) viewDuration = SecondsPerHour;

        var pixelsPerSec = bounds.Width / viewDuration;

        DrawTimePeriodBackgrounds(ctx, pixelsPerSec, viewStartSec, viewEndSec);

        _segmentRects.Clear();

        foreach (var seg in Segments ?? [])
        {
            var segStart = TimeToSeconds(seg.Start);
            var segEnd = TimeToSeconds(seg.End);

            var visibleStart = Math.Max(segStart, viewStartSec);
            var visibleEnd = Math.Min(segEnd, viewEndSec);
            if (visibleEnd <= visibleStart) continue;

            var x = (visibleStart - viewStartSec) * pixelsPerSec;
            var width = (visibleEnd - visibleStart) * pixelsPerSec;
            if (width < MinSegmentWidth) width = MinSegmentWidth;

            var color = TimelineHelpers.ParseColor(seg.Color, DefaultColor);
            var radius = Math.Min(3.0, width / 2.0);
            var rect = new Rect(x, 1, width, bounds.Height - 2);

            // Store for hit-testing
            _segmentRects.Add((seg, rect));

            // 主体色块
            ctx.DrawRectangle(
                new ImmutableSolidColorBrush(color),
                null,
                rect, radius, radius);

            // 顶部高光（模拟 3D 凸起效果）
            var highlightRect = new Rect(x, 1, width, (bounds.Height - 2) * 0.4);
            ctx.DrawRectangle(
                new ImmutableSolidColorBrush(HighlightOverlay),
                null,
                highlightRect, radius, radius);

            // 底部阴影
            var shadowRect = new Rect(x, bounds.Height * 0.6, width, bounds.Height * 0.4);
            ctx.DrawRectangle(
                new ImmutableSolidColorBrush(ShadowOverlay),
                null,
                shadowRect, 0, 0);
        }

        // Hit-test and draw tooltip
        if (_mousePos.HasValue)
        {
            var pos = _mousePos.Value;
            _hoveredHit = _segmentRects.FirstOrDefault(s => s.Bounds.Contains(pos));

            if (_hoveredHit is { } hit && hit.Seg != null)
            {
                DrawSegmentTooltip(ctx, hit.Seg, hit.Bounds);
            }
        }
    }

    private void DrawSegmentTooltip(DrawingContext ctx, MultiTrackSegment seg, Rect segRect)
    {
        var tw = 140;
        var th = 28;
        var p = 6;

        // Position above the segment, centered
        var x = segRect.X + (segRect.Width - tw) / 2;
        x = Math.Max(4, Math.Min(x, Bounds.Width - tw - 4));
        var y = segRect.Y - th - 3;
        if (y < 2) y = segRect.Bottom + 3; // flip below if not enough space above

        ctx.DrawRectangle(_tipShadow, null, new Rect(x + 2, y + 2, tw, th), 4, 4);
        ctx.DrawRectangle(_tipBg, _tipPen, new Rect(x, y, tw, th), 4, 4);

        var startStr = $"{seg.Start:HH:mm}";
        var endStr = $"{seg.End:HH:mm}";
        var dur = seg.End - seg.Start;
        var durStr = dur.TotalHours >= 1
            ? $"{(int)dur.TotalHours}h {dur.Minutes}m"
            : $"{dur.Minutes}m";

        var text = $"{startStr} – {endStr}   {durStr}";
        var label = new FormattedText(text, CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.Medium),
            10, _tipText);

        ctx.DrawText(label, new Point(x + p, y + (th - label.Height) / 2));
    }

    private void DrawTimePeriodBackgrounds(DrawingContext ctx, double pps, double viewStartSec, double viewEndSec)
    {
        var periods = new (int Start, int End, IBrush Brush)[]
        {
            (0, 6, _periodNightBrush),
            (6, 12, _periodMorningBrush),
            (12, 14, _periodNoonBrush),
            (14, 18, _periodAfternoonBrush),
            (18, 24, _periodEveningBrush),
        };

        foreach (var (start, end, brush) in periods)
        {
            var startSec = start * SecondsPerHour;
            var endSec = end * SecondsPerHour;
            if (endSec <= startSec) continue;

            var x1 = (startSec - viewStartSec) * pps;
            var x2 = (endSec - viewStartSec) * pps;

            if (x2 < 0 || x1 > Bounds.Width) continue;

            x1 = Math.Max(0, x1);
            x2 = Math.Min(Bounds.Width, x2);

            if (x2 > x1)
            {
                ctx.DrawRectangle(brush, null, new Rect(x1, 0, x2 - x1, Bounds.Height));
            }
        }
    }

    private static int TimeToSeconds(DateTime t) => t.Hour * 3600 + t.Minute * 60 + t.Second;
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Styling;
using Taix.Client.Shared.Helpers;

namespace Taix.Client.Controls.Timeline;

public class MultiTrackRow : Control
{
    private const int SecondsPerHour = 3600;
    private const double MinSegmentWidth = 3;
    private static readonly Color DefaultColor = Color.Parse("#888888");
    private Color _highlightOverlay;
    private Color _shadowOverlay;

    private IEnumerable<MultiTrackSegment> _segments = Array.Empty<MultiTrackSegment>();
    private double _visibleStartHour = 0.0;
    private double _visibleEndHour = 24.0;

    public static readonly DirectProperty<MultiTrackRow, IEnumerable<MultiTrackSegment>> SegmentsProperty =
        AvaloniaProperty.RegisterDirect<MultiTrackRow, IEnumerable<MultiTrackSegment>>(
            nameof(Segments), o => o.Segments, (o, v) => o.Segments = v);

    public static readonly DirectProperty<MultiTrackRow, double> VisibleStartHourProperty =
        AvaloniaProperty.RegisterDirect<MultiTrackRow, double>(
            nameof(VisibleStartHour), o => o.VisibleStartHour, (o, v) => o.VisibleStartHour = v);

    public static readonly DirectProperty<MultiTrackRow, double> VisibleEndHourProperty =
        AvaloniaProperty.RegisterDirect<MultiTrackRow, double>(
            nameof(VisibleEndHour), o => o.VisibleEndHour, (o, v) => o.VisibleEndHour = v, 24.0);

    private bool _useCategoryColor;
    public static readonly DirectProperty<MultiTrackRow, bool> UseCategoryColorProperty =
        AvaloniaProperty.RegisterDirect<MultiTrackRow, bool>(
            nameof(UseCategoryColor), o => o.UseCategoryColor, (o, v) => o.UseCategoryColor = v);

    public bool UseCategoryColor
    {
        get => _useCategoryColor;
        set => SetAndRaise(UseCategoryColorProperty, ref _useCategoryColor, value);
    }

    public IEnumerable<MultiTrackSegment> Segments
    {
        get => _segments;
        set => SetAndRaise(SegmentsProperty, ref _segments, value);
    }

    public double VisibleStartHour
    {
        get => _visibleStartHour;
        set => SetAndRaise(VisibleStartHourProperty, ref _visibleStartHour, value);
    }

    public double VisibleEndHour
    {
        get => _visibleEndHour;
        set => SetAndRaise(VisibleEndHourProperty, ref _visibleEndHour, value);
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
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        LoadBrushes();
    }

    private void LoadBrushes()
    {
        _tipBg = this.FindResource("TimelineTipBgBrush") as IBrush;
        _tipPen = new Pen((IBrush)this.FindResource(this.ActualThemeVariant, "TimelineTipBorderBrush")!, 1);
        _tipText = (IBrush)this.FindResource(this.ActualThemeVariant, "TimelineTipTextBrush")!;
        _tipShadow = (IBrush)this.FindResource(this.ActualThemeVariant, "TimelineTooltipShadowBrush")!;
        _periodNightBrush = (IBrush)this.FindResource(this.ActualThemeVariant, "TimelinePeriodNightBgBrush")!;
        _periodMorningBrush = (IBrush)this.FindResource(this.ActualThemeVariant, "TimelinePeriodMorningBgBrush")!;
        _periodNoonBrush = (IBrush)this.FindResource(this.ActualThemeVariant, "TimelinePeriodNoonBgBrush")!;
        _periodAfternoonBrush = (IBrush)this.FindResource(this.ActualThemeVariant, "TimelinePeriodAfternoonBgBrush")!;
        _periodEveningBrush = (IBrush)this.FindResource(this.ActualThemeVariant, "TimelinePeriodEveningBgBrush")!;

        var isLight = this.ActualThemeVariant == ThemeVariant.Light;
        _highlightOverlay = isLight
            ? Color.FromArgb(0x28, 0xFF, 0xFF, 0xFF)
            : Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF);
        _shadowOverlay = isLight
            ? Color.FromArgb(0x08, 0x00, 0x00, 0x00)
            : Color.FromArgb(0x0A, 0x00, 0x00, 0x00);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SegmentsProperty
            || change.Property == VisibleStartHourProperty
            || change.Property == VisibleEndHourProperty
            || change.Property == UseCategoryColorProperty)
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

            var displayColor = _useCategoryColor && seg.CategoryColor != null
                ? seg.CategoryColor
                : seg.Color;
            var color = TimelineHelpers.ParseColor(displayColor, DefaultColor);
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
                new ImmutableSolidColorBrush(_highlightOverlay),
                null,
                highlightRect, radius, radius);

            // 底部阴影
            var shadowRect = new Rect(x, bounds.Height * 0.6, width, bounds.Height * 0.4);
            ctx.DrawRectangle(
                new ImmutableSolidColorBrush(_shadowOverlay),
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

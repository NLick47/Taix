using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.VisualTree;

namespace Taix.Client.Controls.Timeline;

public class MultiTrackRow : Control
{
    private const int SecondsPerHour = 3600;
    private const double MinSegmentWidth = 3;
    private static readonly Color DefaultColor = Color.Parse("#888888");

    private static readonly Typeface MediumTypeface = new(FontFamily.Default, FontStyle.Normal, FontWeight.Medium);

    private IEnumerable<MultiTrackSegment> _segments = Array.Empty<MultiTrackSegment>();
    private double _visibleStartHour = 0.0;
    private double _visibleEndHour = 24.0;
    private double? _nowLinePosition;

    private IList<MultiTrackSegment>? _sortedSegments;
    private bool _segmentsNeedSort = true;

    public static readonly DirectProperty<MultiTrackRow, IEnumerable<MultiTrackSegment>> SegmentsProperty =
        AvaloniaProperty.RegisterDirect<MultiTrackRow, IEnumerable<MultiTrackSegment>>(
            nameof(Segments), o => o.Segments, (o, v) => o.Segments = v);

    public static readonly DirectProperty<MultiTrackRow, double> VisibleStartHourProperty =
        AvaloniaProperty.RegisterDirect<MultiTrackRow, double>(
            nameof(VisibleStartHour), o => o.VisibleStartHour, (o, v) => o.VisibleStartHour = v);

    public static readonly DirectProperty<MultiTrackRow, double> VisibleEndHourProperty =
        AvaloniaProperty.RegisterDirect<MultiTrackRow, double>(
            nameof(VisibleEndHour), o => o.VisibleEndHour, (o, v) => o.VisibleEndHour = v, 24.0);

    public static readonly DirectProperty<MultiTrackRow, double?> NowLinePositionProperty =
        AvaloniaProperty.RegisterDirect<MultiTrackRow, double?>(
            nameof(NowLinePosition), o => o.NowLinePosition, (o, v) => o.NowLinePosition = v);

    private TimeRange? _hoveredTimeRange;
    public static readonly DirectProperty<MultiTrackRow, TimeRange?> HoveredTimeRangeProperty =
        AvaloniaProperty.RegisterDirect<MultiTrackRow, TimeRange?>(
            nameof(HoveredTimeRange), o => o.HoveredTimeRange);

    public TimeRange? HoveredTimeRange
    {
        get => _hoveredTimeRange;
        private set => SetAndRaise(HoveredTimeRangeProperty, ref _hoveredTimeRange, value);
    }

    public IEnumerable<MultiTrackSegment> Segments
    {
        get => _segments;
        set
        {
            SetAndRaise(SegmentsProperty, ref _segments, value);
            _segmentsNeedSort = true;
        }
    }

    public double? NowLinePosition
    {
        get => _nowLinePosition;
        set => SetAndRaise(NowLinePositionProperty, ref _nowLinePosition, value);
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

    private struct CachedSegmentInfo
    {
        public Rect Rect;
        public Color Color;
        public double Radius;
    }

    private CachedSegmentInfo[] _cachedSegments = Array.Empty<CachedSegmentInfo>();
    private double _cachedViewStartSec;
    private double _cachedViewEndSec;
    private double _cachedPixelsPerSec;
    private bool _cacheValid;

    private bool _isRowHovered;

    private List<(MultiTrackSegment Seg, Rect Bounds)> _segmentRects = new();

    private IBrush _periodNightBrush = null!;
    private IBrush _periodMorningBrush = null!;
    private IBrush _periodNoonBrush = null!;
    private IBrush _periodAfternoonBrush = null!;
    private IBrush _periodEveningBrush = null!;


    private const double LuminanceThreshold = 0.4;
    private static readonly IBrush LightLabelBrush = new ImmutableSolidColorBrush(Color.FromArgb(0xF0, 0xFF, 0xFF, 0xFF));
    private static readonly IBrush DarkLabelBrush = new ImmutableSolidColorBrush(Color.FromArgb(0xF0, 0x1A, 0x1A, 0x1A));

    // Tooltip related — removed, now using ToolTip.SetTip directly

    public MultiTrackRow()
    {
        PointerEntered += OnPointerEntered;
        PointerExited += OnPointerExited;
        PointerMoved += OnPointerMoved;
    }

    private void OnPointerEntered(object? sender, PointerEventArgs e)
    {
        _isRowHovered = true;
        InvalidateVisual();
    }

    private void OnPointerExited(object? sender, PointerEventArgs e)
    {
        _isRowHovered = false;
        // Clear segment hover when leaving this row
        HoveredTimeRange = null;
        PropagateHoverToTimeline(null);
        InvalidateVisual();
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_segmentsNeedSort)
        {
            _sortedSegments = Segments?.OrderBy(s => TimeToSeconds(s.Start)).ToArray();
            _segmentsNeedSort = false;
        }
        if (_sortedSegments == null || _sortedSegments.Count == 0) return;

        var pos = e.GetPosition(this);
        var snapRadius = 12.0;

        // 计算鼠标位置对应的秒数
        var viewStartSec = Math.Min(VisibleStartHour, VisibleEndHour) * SecondsPerHour;
        var viewEndSec = Math.Max(VisibleStartHour, VisibleEndHour) * SecondsPerHour;
        var viewDuration = viewEndSec - viewStartSec;
        if (viewDuration <= 0) return;

        var pixelsPerSec = Bounds.Width / viewDuration;
        var mouseSec = viewStartSec + pos.X / pixelsPerSec;

        var segs = _sortedSegments;
        var lo = 0;
        var hi = segs.Count - 1;
        while (lo <= hi)
        {
            var mid = (lo + hi) / 2;
            var midStart = TimeToSeconds(segs[mid].Start);
            if (midStart < mouseSec)
                lo = mid + 1;
            else
                hi = mid - 1;
        }

        MultiTrackSegment? nearestSeg = null;
        var nearestDist = double.MaxValue;

        if (lo > 0)
        {
            var seg = segs[lo - 1];
            var segStart = TimeToSeconds(seg.Start);
            var segEnd = TimeToSeconds(seg.End);
            double dist;
            if (mouseSec >= segStart && mouseSec <= segEnd)
                dist = 0;
            else
                dist = Math.Min(Math.Abs(mouseSec - segStart), Math.Abs(mouseSec - segEnd)) * pixelsPerSec;
            if (dist < nearestDist) { nearestDist = dist; nearestSeg = seg; }
        }

        if (lo < segs.Count)
        {
            var seg = segs[lo];
            var segStart = TimeToSeconds(seg.Start);
            var segEnd = TimeToSeconds(seg.End);
            double dist;
            if (mouseSec >= segStart && mouseSec <= segEnd)
                dist = 0;
            else
                dist = Math.Min(Math.Abs(mouseSec - segStart), Math.Abs(mouseSec - segEnd)) * pixelsPerSec;
            if (dist < nearestDist) { nearestDist = dist; nearestSeg = seg; }
        }

        TimeRange? newRange = null;

        if (nearestSeg != null && nearestDist <= snapRadius)
        {
            newRange = new TimeRange(nearestSeg.Start, nearestSeg.End);

            // 优先显示实际使用时长（不含间隙），更符合用户直觉
            var tipText = $"{nearestSeg.Start:HH:mm} - {nearestSeg.End:HH:mm}  ";
            if (nearestSeg.ActualDurationSeconds > 0)
            {
                var actualMinutes = nearestSeg.ActualDurationSeconds / 60;
                var actualSeconds = nearestSeg.ActualDurationSeconds % 60;
                if (actualMinutes >= 1)
                    tipText += $"{actualMinutes}m{actualSeconds}s";
                else
                    tipText += $"{actualSeconds}s";
            }
            else
            {
                var span = nearestSeg.End - nearestSeg.Start;
                if (span.TotalMinutes < 1)
                    tipText += $"{span.Seconds}s";
                else if (span.TotalHours >= 1)
                    tipText += $"{(int)span.TotalHours}h{span.Minutes}m";
                else
                    tipText += $"{span.Minutes}m";
            }

            ToolTip.SetTip(this, tipText);
        }
        else
        {
            ToolTip.SetTip(this, null);
        }

        if (HoveredTimeRange != newRange)
        {
            HoveredTimeRange = newRange;
            PropagateHoverToTimeline(newRange);
        }
    }

    private void PropagateHoverToTimeline(TimeRange? range)
    {
        var parent = this.GetVisualParent();
        while (parent != null)
        {
            if (parent is MultiTrackTimeline timeline)
            {
                timeline.HoveredTimeRange = range;
                return;
            }
            parent = parent.GetVisualParent();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        LoadBrushes();
    }

    private void LoadBrushes()
    {
        _periodNightBrush = (IBrush)this.FindResource(this.ActualThemeVariant, "TimelinePeriodNightBgBrush")!;
        _periodMorningBrush = (IBrush)this.FindResource(this.ActualThemeVariant, "TimelinePeriodMorningBgBrush")!;
        _periodNoonBrush = (IBrush)this.FindResource(this.ActualThemeVariant, "TimelinePeriodNoonBgBrush")!;
        _periodAfternoonBrush = (IBrush)this.FindResource(this.ActualThemeVariant, "TimelinePeriodAfternoonBgBrush")!;
        _periodEveningBrush = (IBrush)this.FindResource(this.ActualThemeVariant, "TimelinePeriodEveningBgBrush")!;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SegmentsProperty
            || change.Property == VisibleStartHourProperty
            || change.Property == VisibleEndHourProperty
            || change.Property == NowLinePositionProperty)
        {
            _cacheValid = false;
            InvalidateVisual();
        }
    }


    private static double RelativeLuminance(Color c)
    {
        static double Linearize(double v)
        {
            v /= 255.0;
            return v <= 0.04045 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Linearize(c.R) + 0.7152 * Linearize(c.G) + 0.0722 * Linearize(c.B);
    }


    private static IBrush PickLabelBrush(Color bg) =>
        RelativeLuminance(bg) > LuminanceThreshold ? DarkLabelBrush : LightLabelBrush;

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

        // 检查缓存是否有效
        if (!_cacheValid
            || _cachedViewStartSec != viewStartSec
            || _cachedViewEndSec != viewEndSec
            || Math.Abs(_cachedPixelsPerSec - pixelsPerSec) > 0.001)
        {
            // 重新计算缓存
            ComputeSegmentCache(viewStartSec, viewEndSec, pixelsPerSec);
            _cachedViewStartSec = viewStartSec;
            _cachedViewEndSec = viewEndSec;
            _cachedPixelsPerSec = pixelsPerSec;
            _cacheValid = true;
        }

        // 绘制缓存的 segments
        var barHeight = bounds.Height * 0.85;
        var barY = (bounds.Height - barHeight) / 2;

        foreach (var info in _cachedSegments)
        {
            var rect = new Rect(info.Rect.X, barY, info.Rect.Width, info.Rect.Height);
            var finalColor = _isRowHovered
                ? LightenColor(info.Color, 0.25)
                : info.Color;
            ctx.DrawRectangle(
                GetCachedBrush(finalColor),
                null,
                rect, info.Radius, info.Radius);
        }
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Color, ImmutableSolidColorBrush> _brushCache = new();

    private static ImmutableSolidColorBrush GetCachedBrush(Color color)
    {
        return _brushCache.GetOrAdd(color, c => new ImmutableSolidColorBrush(c));
    }

    private void ComputeSegmentCache(double viewStartSec, double viewEndSec, double pixelsPerSec)
    {
        var bounds = Bounds;
        var segments = _sortedSegments ?? Segments?.ToList() as IList<MultiTrackSegment>;
        if (segments == null || segments.Count == 0)
        {
            _cachedSegments = Array.Empty<CachedSegmentInfo>();
            return;
        }

        var count = segments.Count;
        if (_cachedSegments.Length != count)
            _cachedSegments = new CachedSegmentInfo[count];

        var idx = 0;
        foreach (var seg in segments)
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
            var barHeight = bounds.Height * 0.85;

            _cachedSegments[idx++] = new CachedSegmentInfo
            {
                Rect = new Rect(x, 0, width, barHeight), // Y 在绘制时加上 barY
                Color = color,
                Radius = radius
            };
        }

        // 如果有跳过的 segments，调整数组
        if (idx < _cachedSegments.Length)
        {
            var trimmed = new CachedSegmentInfo[idx];
            Array.Copy(_cachedSegments, trimmed, idx);
            _cachedSegments = trimmed;
        }
    }

    /// <summary>
    /// Lighten a color by a factor (0 = no change, 1 = white).
    /// </summary>
    private static Color LightenColor(Color c, double factor)
    {
        return Color.FromArgb(c.A,
            (byte)Math.Min(255, c.R + (255 - c.R) * factor),
            (byte)Math.Min(255, c.G + (255 - c.G) * factor),
            (byte)Math.Min(255, c.B + (255 - c.B) * factor));
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

public record TimeRange(DateTime Start, DateTime End);

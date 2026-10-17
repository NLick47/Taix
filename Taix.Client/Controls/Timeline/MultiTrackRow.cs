using System;
using System.Collections.Generic;
using System.Globalization;
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
    private const double MinLabelWidth = 45;
    private static readonly Color DefaultColor = Color.Parse("#888888");

    private static readonly Typeface NormalTypeface = new(FontFamily.Default, FontStyle.Normal, FontWeight.Normal);
    private static readonly Typeface SemiBoldTypeface = new(FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold);

    private IEnumerable<MultiTrackSegment> _segments = Array.Empty<MultiTrackSegment>();
    private double _visibleStartHour = 0.0;
    private double _visibleEndHour = 24.0;

    private IList<MultiTrackSegment>? _sortedSegments;
    private bool _segmentsNeedSort = true;

    private IList<MultiTrackSegment> GetSortedSegments()
    {
        if (_segmentsNeedSort || _sortedSegments == null)
        {
            _sortedSegments = (Segments ?? Array.Empty<MultiTrackSegment>())
                .OrderBy(s => TimeToSeconds(s.Start))
                .ToArray();
            _segmentsNeedSort = false;
        }

        return _sortedSegments;
    }

    public static readonly DirectProperty<MultiTrackRow, IEnumerable<MultiTrackSegment>> SegmentsProperty =
        AvaloniaProperty.RegisterDirect<MultiTrackRow, IEnumerable<MultiTrackSegment>>(
            nameof(Segments), o => o.Segments, (o, v) => o.Segments = v);

    public static readonly DirectProperty<MultiTrackRow, double> VisibleStartHourProperty =
        AvaloniaProperty.RegisterDirect<MultiTrackRow, double>(
            nameof(VisibleStartHour), o => o.VisibleStartHour, (o, v) => o.VisibleStartHour = v);

    public static readonly DirectProperty<MultiTrackRow, double> VisibleEndHourProperty =
        AvaloniaProperty.RegisterDirect<MultiTrackRow, double>(
            nameof(VisibleEndHour), o => o.VisibleEndHour, (o, v) => o.VisibleEndHour = v, 24.0);

    public static readonly StyledProperty<double> CornerRadiusProperty =
        AvaloniaProperty.Register<MultiTrackRow, double>(nameof(CornerRadius));

    public double CornerRadius
    {
        get => GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
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
        public string? Label;
        public MultiTrackSegment? Segment;
    }

    private CachedSegmentInfo[] _cachedSegments = Array.Empty<CachedSegmentInfo>();
    private double _cachedViewStartSec;
    private double _cachedViewEndSec;
    private double _cachedPixelsPerSec;
    private bool _cacheValid;

    private MultiTrackSegment? _hoveredSegment;
    private string? _lastTipText;

    private IBrush _periodNightBrush = null!;
    private IBrush _periodMorningBrush = null!;
    private IBrush _periodNoonBrush = null!;
    private IBrush _periodAfternoonBrush = null!;
    private IBrush _periodEveningBrush = null!;
    private (int Start, int End, IBrush Brush)[] _periodBands = [];

    private const double LuminanceThreshold = 0.35;
    private const double PanDragThreshold = 5;
    private static readonly Cursor PanCursor = new(StandardCursorType.SizeWestEast);
    private static readonly IBrush LightLabelBrush = new ImmutableSolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF));
    private static readonly IBrush DarkLabelBrush = new ImmutableSolidColorBrush(Color.FromArgb(0xFF, 0x1A, 0x1A, 0x1A));
    private static readonly ImmutableSolidColorBrush LightGlowBrush = new(Color.FromArgb(0x30, 0x00, 0x00, 0x00));
    private static readonly ImmutableSolidColorBrush DarkGlowBrush = new(Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF));

    private bool _pointerDown;
    private bool _isPanning;
    private Point _panStartPoint;
    private double _panStartVisibleStart;
    private double _panStartVisibleEnd;

    public MultiTrackRow()
    {
        PointerEntered += OnPointerEntered;
        PointerExited += OnPointerExited;
        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        PointerWheelChanged += OnRowPointerWheel;
        PointerTouchPadGestureMagnify += OnRowMagnify;
    }

    private void OnPointerEntered(object? sender, PointerEventArgs e)
    {
        InvalidateVisual();
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.PointerUpdateKind != PointerUpdateKind.LeftButtonPressed) return;

        _pointerDown = true;
        _isPanning = false;
        _panStartPoint = e.GetPosition(this);
        _panStartVisibleStart = VisibleStartHour;
        _panStartVisibleEnd = VisibleEndHour;
        e.Pointer.Capture(this);
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        var wasPanning = _isPanning;
        _pointerDown = false;
        _isPanning = false;
        Cursor = null;
        e.Pointer.Capture(null);

        // 拖拽平移后的释放不触发点击跳转
        if (wasPanning)
            e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _pointerDown = false;
        _isPanning = false;
        Cursor = null;
    }

    /// <summary>
    /// 在轨道区域内按住水平拖动以平移可视范围（限制在 Frame 边界内）。
    /// </summary>
    private void PanView(Point pos)
    {
        var owner = FindTimelineOwner();
        var bounds = Bounds;
        if (owner == null || bounds.Width <= 0) return;

        var viewDuration = _panStartVisibleEnd - _panStartVisibleStart;
        if (viewDuration <= 0) return;

        var deltaHours = -(pos.X - _panStartPoint.X) / bounds.Width * viewDuration;
        var next = TimelineGestures.Pan(_panStartVisibleStart, _panStartVisibleEnd, deltaHours,
            owner.FrameStartHour, owner.FrameEndHour);

        owner.VisibleStartHour = next.Start;
        owner.VisibleEndHour = next.End;
    }

    private void OnPointerExited(object? sender, PointerEventArgs e)
    {
        _hoveredSegment = null;
        if (_lastTipText != null)
        {
            _lastTipText = null;
            ToolTip.SetTip(this, null);
        }
        InvalidateVisual();
    }

    private MultiTrackTimeline? FindTimelineOwner()
    {
        var parent = this.GetVisualParent();
        while (parent != null)
        {
            if (parent is MultiTrackTimeline timeline) return timeline;
            parent = parent.GetVisualParent();
        }
        return null;
    }

    private void OnRowPointerWheel(object? sender, PointerWheelEventArgs e)
    {
        var owner = FindTimelineOwner();
        if (owner == null) return;

        var isZoomModifier = e.KeyModifiers.HasFlag(KeyModifiers.Meta)
                             || e.KeyModifiers.HasFlag(KeyModifiers.Control);

        var isZoom = isZoomModifier || !TimelineGestures.IsHorizontalDominant(e.Delta);

        var pos = e.GetPosition(this);
        var d = TimelineGestures.GetDominantDelta(e.Delta);

        var next = isZoom
            ? TimelineGestures.ZoomAt(owner.VisibleStartHour, owner.VisibleEndHour,
                pos.X, Bounds.Width, TimelineGestures.GetWheelZoomFactor(d),
                owner.FrameStartHour, owner.FrameEndHour, TimelineGestures.MinVisibleHours)
            : TimelineGestures.PanByWheel(owner.VisibleStartHour, owner.VisibleEndHour,
                e.Delta, owner.FrameStartHour, owner.FrameEndHour);

        owner.VisibleStartHour = next.Start;
        owner.VisibleEndHour = next.End;
        e.Handled = true;
    }

    private void OnRowMagnify(object? sender, PointerDeltaEventArgs e)
    {
        var owner = FindTimelineOwner();
        if (owner == null) return;

        var pos = e.GetPosition(this);
        var next = TimelineGestures.ZoomAt(owner.VisibleStartHour, owner.VisibleEndHour,
            pos.X, Bounds.Width, TimelineGestures.GetMagnifyFactor(e.Delta.Y),
            owner.FrameStartHour, owner.FrameEndHour, TimelineGestures.MinVisibleHours);

        owner.VisibleStartHour = next.Start;
        owner.VisibleEndHour = next.End;
        e.Handled = true;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        var pos = e.GetPosition(this);

        if (_pointerDown)
        {
            if (!_isPanning)
            {
                var dx = pos.X - _panStartPoint.X;
                var dy = pos.Y - _panStartPoint.Y;
                if (Math.Abs(dx) >= PanDragThreshold && Math.Abs(dx) > Math.Abs(dy))
                {
                    _isPanning = true;
                    Cursor = PanCursor;
                    _hoveredSegment = null;
                    if (_lastTipText != null)
                    {
                        _lastTipText = null;
                        ToolTip.SetTip(this, null);
                    }
                }
            }

            if (_isPanning)
            {
                PanView(pos);
                return;
            }
        }

        var segs = GetSortedSegments();
        if (segs.Count == 0) return;

        var snapRadius = 12.0;

        var viewStartSec = Math.Min(VisibleStartHour, VisibleEndHour) * SecondsPerHour;
        var viewEndSec = Math.Max(VisibleStartHour, VisibleEndHour) * SecondsPerHour;
        var viewDuration = viewEndSec - viewStartSec;
        if (viewDuration <= 0) return;

        var pixelsPerSec = Bounds.Width / viewDuration;
        var mouseSec = viewStartSec + pos.X / pixelsPerSec;

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

        var changed = _hoveredSegment != nearestSeg || (nearestSeg != null && nearestDist > snapRadius);
        _hoveredSegment = nearestSeg != null && nearestDist <= snapRadius ? nearestSeg : null;

        var tipText = _hoveredSegment != null ? BuildSegmentTip(_hoveredSegment) : null;

        if (!string.Equals(_lastTipText, tipText, StringComparison.Ordinal))
        {
            _lastTipText = tipText;
            ToolTip.SetTip(this, tipText);
        }

        if (changed)
            InvalidateVisual();
    }

    /// <summary>
    /// 构建片段悬停提示（缓存判断委托给调用方，避免重复设置 ToolTip）。
    /// </summary>
    private static string BuildSegmentTip(MultiTrackSegment segment)
    {
        var tipText = $"{segment.Start:HH:mm} - {segment.End:HH:mm}  ";
        if (segment.ActualDurationSeconds > 0)
        {
            var actualMinutes = segment.ActualDurationSeconds / 60;
            var actualSeconds = segment.ActualDurationSeconds % 60;
            return tipText + (actualMinutes >= 1 ? $"{actualMinutes}m{actualSeconds}s" : $"{actualSeconds}s");
        }

        var span = segment.End - segment.Start;
        if (span.TotalMinutes < 1)
            return tipText + $"{span.Seconds}s";
        if (span.TotalHours >= 1)
            return tipText + $"{(int)span.TotalHours}h{span.Minutes}m";
        return tipText + $"{span.Minutes}m";
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
        _periodBands =
        [
            (0, 6, _periodNightBrush),
            (6, 12, _periodMorningBrush),
            (12, 14, _periodNoonBrush),
            (14, 18, _periodAfternoonBrush),
            (18, 24, _periodEveningBrush),
        ];

        // 主题可能已变：释放旧主题下的 FormattedText 缓存条目
        _labelCache.Clear();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SegmentsProperty
            || change.Property == VisibleStartHourProperty
            || change.Property == VisibleEndHourProperty)
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

        if (CornerRadius > 0)
        {
            using (ctx.PushClip(new RoundedRect(new Rect(bounds.Size), CornerRadius, CornerRadius)))
            {
                RenderContent(ctx, bounds);
            }
        }
        else
        {
            RenderContent(ctx, bounds);
        }
    }

    private void RenderContent(DrawingContext ctx, Rect bounds)
    {
        var viewStartSec = Math.Min(VisibleStartHour, VisibleEndHour) * SecondsPerHour;
        var viewEndSec = Math.Max(VisibleStartHour, VisibleEndHour) * SecondsPerHour;
        var viewDuration = viewEndSec - viewStartSec;
        if (viewDuration <= 0) viewDuration = SecondsPerHour;

        var pixelsPerSec = bounds.Width / viewDuration;

        DrawTimePeriodBackgrounds(ctx, pixelsPerSec, viewStartSec);

        if (!_cacheValid
            || _cachedViewStartSec != viewStartSec
            || _cachedViewEndSec != viewEndSec
            || Math.Abs(_cachedPixelsPerSec - pixelsPerSec) > 0.001)
        {
            ComputeSegmentCache(viewStartSec, viewEndSec, pixelsPerSec);
            _cachedViewStartSec = viewStartSec;
            _cachedViewEndSec = viewEndSec;
            _cachedPixelsPerSec = pixelsPerSec;
            _cacheValid = true;
        }

        var barHeight = bounds.Height * 0.85;
        var barY = (bounds.Height - barHeight) / 2;

        var hasHover = _hoveredSegment != null;

        foreach (var info in _cachedSegments)
        {
            var rect = new Rect(info.Rect.X, barY, info.Rect.Width, info.Rect.Height);
            var finalColor = info.Color;

            if (hasHover)
            {
                var isHoveredSeg = info.Segment != null && info.Segment == _hoveredSegment;
                finalColor = isHoveredSeg
                    ? LightenColor(info.Color, 0.25)
                    : DarkenColor(info.Color, 0.15);
            }

            ctx.DrawRectangle(
                GetCachedBrush(finalColor),
                null,
                rect, info.Radius, info.Radius);

            if (info.Label != null && info.Rect.Width >= MinLabelWidth)
            {
                var labelSize = info.Rect.Width >= 100 ? 11 : 10;
                var useLight = RelativeLuminance(finalColor) > LuminanceThreshold;
                var labelBrush = useLight ? DarkLabelBrush : LightLabelBrush;
                var glowBrush = useLight ? DarkGlowBrush : LightGlowBrush;
                var label = GetCachedLabel(info.Label, labelSize, labelBrush);
                var lx = info.Rect.X + (info.Rect.Width - label.Width) / 2;
                var ly = barY + (info.Rect.Height - label.Height) / 2;
                if (lx < 2) lx = 2;
                if (lx + label.Width > bounds.Width - 2)
                    lx = bounds.Width - 2 - label.Width;
                var glow = GetCachedLabel(info.Label, labelSize, glowBrush);
                for (var dx = -1; dx <= 1; dx++)
                for (var dy = -1; dy <= 1; dy++)
                    ctx.DrawText(glow, new Point(lx + dx, ly + dy));
                ctx.DrawText(label, new Point(lx, ly));
            }
        }
    }

    private static string FormatDurationText(int seconds)
    {
        if (seconds < 60) return $"{seconds}s";
        var minutes = seconds / 60;
        var secs = seconds % 60;
        if (minutes >= 60)
        {
            var h = minutes / 60;
            var m = minutes % 60;
            return m > 0 ? $"{h}h{m}m" : $"{h}h";
        }
        return secs > 0 ? $"{minutes}m{secs}s" : $"{minutes}m";
    }

    private readonly Dictionary<(string, int, Color), FormattedText> _labelCache = new();

    private FormattedText GetCachedLabel(string text, int size, IBrush brush)
    {
        var key = (text, size, (brush as ISolidColorBrush)?.Color ?? Colors.White);
        if (_labelCache.TryGetValue(key, out var cached))
            return cached;
        var ft = new FormattedText(text, CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, NormalTypeface, size, brush);
        _labelCache[key] = ft;
        return ft;
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Color, ImmutableSolidColorBrush> _brushCache = new();

    private static ImmutableSolidColorBrush GetCachedBrush(Color color)
    {
        return _brushCache.GetOrAdd(color, c => new ImmutableSolidColorBrush(c));
    }

    private void ComputeSegmentCache(double viewStartSec, double viewEndSec, double pixelsPerSec)
    {
        var bounds = Bounds;
        var segments = GetSortedSegments();
        if (segments.Count == 0)
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

            string? label = null;
            if (width >= MinLabelWidth)
            {
                var secs = seg.ActualDurationSeconds > 0
                    ? seg.ActualDurationSeconds
                    : (int)(seg.End - seg.Start).TotalSeconds;
                label = FormatDurationText(secs);
            }

            _cachedSegments[idx++] = new CachedSegmentInfo
            {
                Rect = new Rect(x, 0, width, barHeight),
                Color = color,
                Radius = radius,
                Label = label,
                Segment = seg
            };
        }

        if (idx < _cachedSegments.Length)
        {
            var trimmed = new CachedSegmentInfo[idx];
            Array.Copy(_cachedSegments, trimmed, idx);
            _cachedSegments = trimmed;
        }
    }

    private static Color LightenColor(Color c, double factor)
    {
        return Color.FromArgb(c.A,
            (byte)Math.Min(255, c.R + (255 - c.R) * factor),
            (byte)Math.Min(255, c.G + (255 - c.G) * factor),
            (byte)Math.Min(255, c.B + (255 - c.B) * factor));
    }

    private static Color DarkenColor(Color c, double factor)
    {
        return Color.FromArgb(c.A,
            (byte)Math.Max(0, c.R * (1 - factor)),
            (byte)Math.Max(0, c.G * (1 - factor)),
            (byte)Math.Max(0, c.B * (1 - factor)));
    }

    private void DrawTimePeriodBackgrounds(DrawingContext ctx, double pps, double viewStartSec)
    {
        foreach (var (start, end, brush) in _periodBands)
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

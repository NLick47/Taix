using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Taix.Client.Logging;
using Taix.Client.Shared.Helpers;

namespace Taix.Client.Controls.Timeline;

public class MultiTrackTimeRuler : Control
{
    private const int SecondsPerHour = 3600;
    private const double MinZoomHours = 0.25;
    private const double ZoomFactor = 1.3;
    private const double MinDragDistance = 5;

    private static readonly Typeface NormalTypeface = new(FontFamily.Default, FontStyle.Normal, FontWeight.Normal);

    private readonly Dictionary<(string Text, int Size), FormattedText> _labelCache = new();

    private double _visibleStartHour = 0.0;
    private double _visibleEndHour = 24.0;
    private double _boundStartHour = 0.0;
    private double _boundEndHour = 24.0;
    private DateTime _date = DateTime.Today;

    private bool _isPanning;
    private Point _dragStart;
    private double _dragStartHour;
    private double _dragEndHour;

    public static readonly DirectProperty<MultiTrackTimeRuler, double> VisibleStartHourProperty =
        AvaloniaProperty.RegisterDirect<MultiTrackTimeRuler, double>(
            nameof(VisibleStartHour), o => o.VisibleStartHour, (o, v) => o.VisibleStartHour = v);

    public static readonly DirectProperty<MultiTrackTimeRuler, double> VisibleEndHourProperty =
        AvaloniaProperty.RegisterDirect<MultiTrackTimeRuler, double>(
            nameof(VisibleEndHour), o => o.VisibleEndHour, (o, v) => o.VisibleEndHour = v, 24.0);

    public static readonly DirectProperty<MultiTrackTimeRuler, DateTime> DateProperty =
        AvaloniaProperty.RegisterDirect<MultiTrackTimeRuler, DateTime>(
            nameof(Date), o => o.Date, (o, v) => o.Date = v);

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

    public DateTime Date
    {
        get => _date;
        set => SetAndRaise(DateProperty, ref _date, value);
    }

    public static readonly DirectProperty<MultiTrackTimeRuler, double> BoundStartHourProperty =
        AvaloniaProperty.RegisterDirect<MultiTrackTimeRuler, double>(
            nameof(BoundStartHour), o => o.BoundStartHour, (o, v) => o.BoundStartHour = v);

    public static readonly DirectProperty<MultiTrackTimeRuler, double> BoundEndHourProperty =
        AvaloniaProperty.RegisterDirect<MultiTrackTimeRuler, double>(
            nameof(BoundEndHour), o => o.BoundEndHour, (o, v) => o.BoundEndHour = v, 24.0);

    public double BoundStartHour
    {
        get => _boundStartHour;
        set => SetAndRaise(BoundStartHourProperty, ref _boundStartHour, value);
    }

    public double BoundEndHour
    {
        get => _boundEndHour;
        set => SetAndRaise(BoundEndHourProperty, ref _boundEndHour, value);
    }

    private IPen _tickMajor = null!;
    private IPen _tickMinor = null!;
    private IBrush _tickText = null!;
    private IPen _nowPen = null!;
    private IBrush _nowDotBrush = null!;
    private IBrush _periodNightBrush = null!;
    private IBrush _periodMorningBrush = null!;
    private IBrush _periodNoonBrush = null!;
    private IBrush _periodAfternoonBrush = null!;
    private IBrush _periodEveningBrush = null!;

    public MultiTrackTimeRuler()
    {
        ClipToBounds = true;
        Height = 24;
        Cursor = Cursor.Parse("Hand");

        PointerWheelChanged += OnPointerWheelChanged;
        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        LoadBrushes();
    }

    private void LoadBrushes()
    {
        _tickMajor = new Pen((IBrush)this.FindResource(this.ActualThemeVariant, "TimelineTickMajorBrush")!, 1);
        _tickMinor = new Pen((IBrush)this.FindResource(this.ActualThemeVariant, "TimelineTickHalfBrush")!, 0.5);
        _tickText = (IBrush)this.FindResource(this.ActualThemeVariant, "TimelineTickTextBrush")!;
        _nowPen = new Pen((IBrush)this.FindResource(this.ActualThemeVariant, "TimelineNowBrush")!, 1.5);
        _nowDotBrush = (IBrush)this.FindResource(this.ActualThemeVariant, "TimelineNowDotOuterBrush")!;
        _periodNightBrush = (IBrush)this.FindResource(this.ActualThemeVariant, "TimelinePeriodNightBgBrush")!;
        _periodMorningBrush = (IBrush)this.FindResource(this.ActualThemeVariant, "TimelinePeriodMorningBgBrush")!;
        _periodNoonBrush = (IBrush)this.FindResource(this.ActualThemeVariant, "TimelinePeriodNoonBgBrush")!;
        _periodAfternoonBrush = (IBrush)this.FindResource(this.ActualThemeVariant, "TimelinePeriodAfternoonBgBrush")!;
        _periodEveningBrush = (IBrush)this.FindResource(this.ActualThemeVariant, "TimelinePeriodEveningBgBrush")!;

        _labelCache.Clear();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == VisibleStartHourProperty
            || change.Property == VisibleEndHourProperty
            || change.Property == DateProperty)
        {
            InvalidateVisual();
        }
    }

    public override void Render(DrawingContext ctx)
    {
        var bounds = Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        var viewStartSec = Math.Min(VisibleStartHour, VisibleEndHour) * SecondsPerHour;
        var viewEndSec = Math.Max(VisibleStartHour, VisibleEndHour) * SecondsPerHour;
        var viewDuration = viewEndSec - viewStartSec;
        if (viewDuration <= 0) viewDuration = SecondsPerHour;

        var pps = bounds.Width / viewDuration;
        var pixelsPerHour = SecondsPerHour * pps;

        // Draw time period backgrounds
        DrawTimePeriodBackgrounds(ctx, pps, viewStartSec, viewEndSec);

        // Determine tick interval based on zoom level
        int majorIntervalHours = 1;
        if (pixelsPerHour < 40) majorIntervalHours = 4;
        else if (pixelsPerHour < 80) majorIntervalHours = 2;
        else if (pixelsPerHour < 160) majorIntervalHours = 1;

        int minorIntervalMinutes = 30;
        if (pixelsPerHour > 480) minorIntervalMinutes = 5;
        else if (pixelsPerHour > 240) minorIntervalMinutes = 10;
        else if (pixelsPerHour > 120) minorIntervalMinutes = 15;
        else if (pixelsPerHour < 60) minorIntervalMinutes = 60;

        // Draw ticks
        var totalHours = viewEndSec / SecondsPerHour + 1;
        var font = NormalTypeface;
        var bottomY = bounds.Height;
        var labelY = 2.0;

        for (var h = 0; h <= totalHours; h++)
        {
            var hourSec = h * SecondsPerHour;
            if (hourSec > viewEndSec) continue;

            var x = (hourSec - viewStartSec) * pps;
            if (x < -2 || x > bounds.Width + 2) continue;

            var isMajor = h % majorIntervalHours == 0;

            if (isMajor)
            {
                ctx.DrawLine(_tickMajor, new Point(x, 6), new Point(x, bottomY));

                // 使用缓存的 FormattedText
                var label = GetCachedTimeRulerLabel($"{h}:00", 9);
                var labelX = Math.Max(2, x + 4);
                if (labelX + label.Width > bounds.Width - 2)
                    labelX = Math.Max(2, bounds.Width - 2 - label.Width);
                ctx.DrawText(label, new Point(labelX, labelY));
            }
            else
            {
                ctx.DrawLine(_tickMinor, new Point(x, 12), new Point(x, bottomY));
            }

            // Minor ticks between hours
            if (h >= totalHours) continue;
            var stepSec = minorIntervalMinutes * 60;
            for (var s = stepSec; s < SecondsPerHour; s += stepSec)
            {
                var sx = (h * SecondsPerHour + s - viewStartSec) * pps;
                if (sx >= 0 && sx <= bounds.Width)
                    ctx.DrawLine(_tickMinor, new Point(sx, 16), new Point(sx, bottomY));
            }
        }

        // Draw minute labels when zoomed in enough
        DrawMinuteLabels(ctx, pps, viewStartSec, viewEndSec);

        // Draw "now" line
        var now = DateTime.Now;
        if (now.Date == Date.Date)
        {
            var nowSec = now.Hour * 3600 + now.Minute * 60 + now.Second;
            if (nowSec >= viewStartSec && nowSec <= viewEndSec)
            {
                var nx = (nowSec - viewStartSec) * pps;
                ctx.DrawLine(_nowPen, new Point(nx, 2), new Point(nx, bottomY));
                ctx.DrawRectangle(_nowDotBrush, null, new Rect(nx - 3, 2, 6, 6), 3, 3);
                ctx.DrawRectangle(_nowPen.Brush, null, new Rect(nx - 2.5, 2.5, 5, 5), 2.5, 2.5);
            }
        }
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

    private void DrawMinuteLabels(DrawingContext ctx, double pps, double viewStartSec, double viewEndSec)
    {
        var pixelsPerMinute = 60 * pps;
        if (pixelsPerMinute < 6) return;

        int labelIntervalMinutes;
        if (pixelsPerMinute > 18) labelIntervalMinutes = 5;
        else if (pixelsPerMinute > 9) labelIntervalMinutes = 10;
        else labelIntervalMinutes = 15;

        var stepSeconds = labelIntervalMinutes * 60;
        var font = NormalTypeface;
        var maxHour = (int)(viewEndSec / SecondsPerHour) + 1;
        var minHour = Math.Max(0, (int)(viewStartSec / SecondsPerHour));

        for (var h = minHour; h < maxHour; h++)
        {
            for (var s = stepSeconds; s < SecondsPerHour; s += stepSeconds)
            {
                var sx = (h * SecondsPerHour + s - viewStartSec) * pps;
                if (sx < -20 || sx > Bounds.Width + 20) continue;

                var minutes = s / 60;
                var labelText = $"{h}:{minutes:D2}";

                var label = GetCachedTimeRulerLabel(labelText, 8);

                var labelX = sx - label.Width / 2;
                if (labelX < 2) labelX = 2;
                if (labelX + label.Width > Bounds.Width - 2) labelX = Bounds.Width - 2 - label.Width;

                ctx.DrawText(label, new Point(labelX, 13));
            }
        }
    }

    private FormattedText GetCachedTimeRulerLabel(string text, int size)
    {
        var key = (text, size);
        if (_labelCache.TryGetValue(key, out var cached))
            return cached;

        var ft = new FormattedText(text, CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, NormalTypeface, size, _tickText);
        _labelCache[key] = ft;
        return ft;
    }

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        var pos = e.GetPosition(this);
        var bounds = Bounds;

        var viewStart = Math.Min(VisibleStartHour, VisibleEndHour);
        var viewEnd = Math.Max(VisibleStartHour, VisibleEndHour);
        var viewDuration = viewEnd - viewStart;
        if (viewDuration <= 0) viewDuration = 24;

        var mouseHour = viewStart + (pos.X / bounds.Width) * viewDuration;

        var k = e.Delta.Y > 0 ? 1.0 / ZoomFactor : ZoomFactor;
        var boundStart = Math.Min(BoundStartHour, BoundEndHour);
        var boundEnd = Math.Max(BoundStartHour, BoundEndHour);
        var boundDuration = boundEnd - boundStart;
        if (boundDuration <= 0) { boundStart = 0; boundEnd = 24; boundDuration = 24; }

        var newDuration = Math.Clamp(viewDuration * k, MinZoomHours, boundDuration);

        var newStart = mouseHour - (mouseHour - viewStart) * (newDuration / viewDuration);
        var newEnd = newStart + newDuration;

        if (newStart < boundStart) { newStart = boundStart; newEnd = boundStart + newDuration; }
        if (newEnd > boundEnd) { newEnd = boundEnd; newStart = boundEnd - newDuration; }
        if (newStart < boundStart) { newStart = boundStart; newEnd = boundEnd; }

        VisibleStartHour = newStart;
        VisibleEndHour = newEnd;

        PropagateToTimeline(newStart, newEnd);
        e.Handled = true;
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.PointerUpdateKind == PointerUpdateKind.LeftButtonPressed)
        {
            _isPanning = true;
            _dragStart = e.GetPosition(this);
            _dragStartHour = VisibleStartHour;
            _dragEndHour = VisibleEndHour;
            e.Pointer.Capture(this);
            e.Handled = true;
        }
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isPanning) return;

        var pos = e.GetPosition(this);
        var bounds = Bounds;
        var dx = pos.X - _dragStart.X;

        var viewDuration = _dragEndHour - _dragStartHour;
        if (viewDuration <= 0) return;

        var boundStart = Math.Min(BoundStartHour, BoundEndHour);
        var boundEnd = Math.Max(BoundStartHour, BoundEndHour);

        var hourDelta = -dx / bounds.Width * viewDuration;
        var newStart = _dragStartHour + hourDelta;
        var newEnd = _dragEndHour + hourDelta;

        if (newStart < boundStart) { newStart = boundStart; newEnd = boundStart + viewDuration; }
        if (newEnd > boundEnd) { newEnd = boundEnd; newStart = boundEnd - viewDuration; }

        VisibleStartHour = newStart;
        VisibleEndHour = newEnd;

        PropagateToTimeline(newStart, newEnd);
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_isPanning)
        {
            _isPanning = false;
            e.Pointer.Capture(null);
        }
    }

    private void PropagateToTimeline(double startHour, double endHour)
    {
        var parent = this.GetVisualParent();
        while (parent != null)
        {
            if (parent is MultiTrackTimeline timeline)
            {
                timeline.VisibleStartHour = startHour;
                timeline.VisibleEndHour = endHour;
                return;
            }
            parent = parent.GetVisualParent();
        }
    }
}

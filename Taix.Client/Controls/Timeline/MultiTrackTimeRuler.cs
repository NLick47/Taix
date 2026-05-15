using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Taix.Client.Controls.Timeline;

public class MultiTrackTimeRuler : Control
{
    private const int SecondsPerHour = 3600;

    private double _visibleStartHour = 0.0;
    private double _visibleEndHour = 24.0;
    private DateTime _date = DateTime.Today;

    public static readonly DirectProperty<MultiTrackTimeRuler, double> VisibleStartHourProperty =
        AvaloniaProperty.RegisterDirect<MultiTrackTimeRuler, double>(
            nameof(VisibleStartHour), o => o._visibleStartHour, (o, v) => o._visibleStartHour = v);

    public static readonly DirectProperty<MultiTrackTimeRuler, double> VisibleEndHourProperty =
        AvaloniaProperty.RegisterDirect<MultiTrackTimeRuler, double>(
            nameof(VisibleEndHour), o => o._visibleEndHour, (o, v) => o._visibleEndHour = v, 24.0);

    public static readonly DirectProperty<MultiTrackTimeRuler, DateTime> DateProperty =
        AvaloniaProperty.RegisterDirect<MultiTrackTimeRuler, DateTime>(
            nameof(Date), o => o._date, (o, v) => o._date = v);

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
        LoadBrushes();
    }

    private void LoadBrushes()
    {
        _tickMajor = new Pen(
            Application.Current?.FindResource("TimelineTickMajorBrush") as IBrush
            ?? new SolidColorBrush(Color.Parse("#555566")), 1);
        _tickMinor = new Pen(
            Application.Current?.FindResource("TimelineTickHalfBrush") as IBrush
            ?? new SolidColorBrush(Color.Parse("#3a3a48")), 0.5);
        _tickText = Application.Current?.FindResource("TimelineTickTextBrush") as IBrush
                    ?? new SolidColorBrush(Color.Parse("#8b949e"));
        _nowPen = new Pen(
            Application.Current?.FindResource("TimelineNowBrush") as IBrush
            ?? new SolidColorBrush(Color.Parse("#58a6ff")), 1.5);
        _nowDotBrush = Application.Current?.FindResource("TimelineNowDotOuterBrush") as IBrush
                       ?? new SolidColorBrush(Colors.White);
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
        var font = new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.Normal);
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

                var label = new FormattedText($"{h:D2}:00", CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight, font, 9, _tickText);
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
        var font = new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.Normal);
        var maxHour = (int)(viewEndSec / SecondsPerHour) + 1;
        var minHour = Math.Max(0, (int)(viewStartSec / SecondsPerHour));

        for (var h = minHour; h < maxHour; h++)
        {
            for (var s = stepSeconds; s < SecondsPerHour; s += stepSeconds)
            {
                var sx = (h * SecondsPerHour + s - viewStartSec) * pps;
                if (sx < -20 || sx > Bounds.Width + 20) continue;

                var minutes = s / 60;
                var labelText = labelIntervalMinutes == 5 && minutes % 15 != 0
                    ? $"{minutes:D2}"
                    : $":{minutes:D2}";

                var label = new FormattedText(labelText, CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight, font, 8, _tickText);

                var labelX = sx - label.Width / 2;
                if (labelX < 2) labelX = 2;
                if (labelX + label.Width > Bounds.Width - 2) labelX = Bounds.Width - 2 - label.Width;

                ctx.DrawText(label, new Point(labelX, 13));
            }
        }
    }
}

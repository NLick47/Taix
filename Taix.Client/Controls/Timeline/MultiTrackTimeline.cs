using System;
using System.Collections.Generic;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Taix.Client.Controls.Timeline;

public class MultiTrackTimeline : TemplatedControl
{
    private const int SecondsPerHour = 3600;

    private IEnumerable<MultiTrackTimelineItem> _dataItems = Array.Empty<MultiTrackTimelineItem>();
    private DateTime _date = DateTime.Now;
    private double _visibleStartHour = 0.0;
    private double _visibleEndHour = 24.0;
    private ListBox? _listBox;
    private DispatcherTimer? _nowTimer;
    private double? _nowLinePosition;

    public static readonly DirectProperty<MultiTrackTimeline, double?> NowLinePositionProperty =
        AvaloniaProperty.RegisterDirect<MultiTrackTimeline, double?>(
            nameof(NowLinePosition), o => o.NowLinePosition);

    public static readonly StyledProperty<TimeRange?> HoveredTimeRangeProperty =
        AvaloniaProperty.Register<MultiTrackTimeline, TimeRange?>(nameof(HoveredTimeRange));

    public double? NowLinePosition
    {
        get => _nowLinePosition;
        private set => SetAndRaise(NowLinePositionProperty, ref _nowLinePosition, value);
    }

    public TimeRange? HoveredTimeRange
    {
        get => GetValue(HoveredTimeRangeProperty);
        set => SetValue(HoveredTimeRangeProperty, value);
    }

    public static readonly DirectProperty<MultiTrackTimeline, IEnumerable<MultiTrackTimelineItem>> DataItemsProperty =
        AvaloniaProperty.RegisterDirect<MultiTrackTimeline, IEnumerable<MultiTrackTimelineItem>>(
            nameof(DataItems), o => o.DataItems, (o, v) => o.DataItems = v);

    public static readonly DirectProperty<MultiTrackTimeline, DateTime> DateProperty =
        AvaloniaProperty.RegisterDirect<MultiTrackTimeline, DateTime>(
            nameof(Date), o => o.Date, (o, v) => o.Date = v);

    public static readonly DirectProperty<MultiTrackTimeline, double> VisibleStartHourProperty =
        AvaloniaProperty.RegisterDirect<MultiTrackTimeline, double>(
            nameof(VisibleStartHour), o => o.VisibleStartHour, (o, v) => o.VisibleStartHour = v);

    public static readonly DirectProperty<MultiTrackTimeline, double> VisibleEndHourProperty =
        AvaloniaProperty.RegisterDirect<MultiTrackTimeline, double>(
            nameof(VisibleEndHour), o => o.VisibleEndHour, (o, v) => o.VisibleEndHour = v, 24.0);

    public static readonly StyledProperty<ICommand?> ClickCommandProperty =
        AvaloniaProperty.Register<MultiTrackTimeline, ICommand?>(nameof(ClickCommand));

    public IEnumerable<MultiTrackTimelineItem> DataItems
    {
        get => _dataItems;
        set => SetAndRaise(DataItemsProperty, ref _dataItems, value);
    }

    public DateTime Date
    {
        get => _date;
        set => SetAndRaise(DateProperty, ref _date, value);
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

    public ICommand? ClickCommand
    {
        get => GetValue(ClickCommandProperty);
        set => SetValue(ClickCommandProperty, value);
    }

    public MultiTrackTimeline()
    {
        _nowTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        _nowTimer.Tick += OnNowTimerTick;
        UpdateNowLinePosition();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _nowTimer?.Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _nowTimer?.Stop();
    }

    private void OnNowTimerTick(object? sender, EventArgs e)
    {
        UpdateNowLinePosition();
    }

    private void UpdateNowLinePosition()
    {
        var now = DateTime.Now;
        if (now.Date != Date.Date)
        {
            NowLinePosition = null;
            return;
        }

        var nowSec = now.Hour * SecondsPerHour + now.Minute * 60 + now.Second;
        var viewStartSec = Math.Min(VisibleStartHour, VisibleEndHour) * SecondsPerHour;
        var viewEndSec = Math.Max(VisibleStartHour, VisibleEndHour) * SecondsPerHour;
        var viewDuration = viewEndSec - viewStartSec;
        if (viewDuration <= 0) viewDuration = SecondsPerHour;

        if (nowSec < viewStartSec || nowSec > viewEndSec)
        {
            NowLinePosition = null;
            return;
        }

        // We don't have Bounds.Width here, use 0 and let the row compute from its own bounds
        // Actually compute proportionally: the row will use its own Bounds.Width
        NowLinePosition = (nowSec - viewStartSec) / viewDuration;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (_listBox != null)
            _listBox.PointerReleased -= OnListBoxPointerReleased;

        _listBox = e.NameScope.Find<ListBox>("PART_ItemsControl");

        if (_listBox != null)
            _listBox.PointerReleased += OnListBoxPointerReleased;
    }

    private void OnListBoxPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton != MouseButton.Left || ClickCommand is not { } cmd)
            return;

        var visual = e.Source as Visual;
        while (visual != null)
        {
            if (visual is ListBoxItem { DataContext: MultiTrackTimelineItem item })
            {
                if (cmd.CanExecute(item))
                    cmd.Execute(item);
                return;
            }
            visual = visual.GetVisualParent();
        }
    }
}

using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace Taix.Client.Controls.Timeline;

public class MultiTrackTimeline : TemplatedControl
{
    private IEnumerable<MultiTrackTimelineItem> _dataItems = Array.Empty<MultiTrackTimelineItem>();
    private DateTime _date = DateTime.Now;
    private double _visibleStartHour = 0.0;
    private double _visibleEndHour = 24.0;

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

    public MultiTrackTimeline()
    {
    }
}

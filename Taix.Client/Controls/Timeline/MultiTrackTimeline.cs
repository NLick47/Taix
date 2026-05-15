using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace Taix.Client.Controls.Timeline;

public class MultiTrackTimeline : TemplatedControl
{
    public static readonly StyledProperty<IEnumerable<MultiTrackTimelineItem>> DataItemsProperty =
        AvaloniaProperty.Register<MultiTrackTimeline, IEnumerable<MultiTrackTimelineItem>>(nameof(DataItems), Array.Empty<MultiTrackTimelineItem>());

    public static readonly StyledProperty<DateTime> DateProperty =
        AvaloniaProperty.Register<MultiTrackTimeline, DateTime>(nameof(Date), DateTime.Now);

    public static readonly StyledProperty<double> VisibleStartHourProperty =
        AvaloniaProperty.Register<MultiTrackTimeline, double>(nameof(VisibleStartHour), 0.0);

    public static readonly StyledProperty<double> VisibleEndHourProperty =
        AvaloniaProperty.Register<MultiTrackTimeline, double>(nameof(VisibleEndHour), 24.0);

    public IEnumerable<MultiTrackTimelineItem> DataItems
    {
        get => GetValue(DataItemsProperty);
        set => SetValue(DataItemsProperty, value);
    }

    public DateTime Date
    {
        get => GetValue(DateProperty);
        set => SetValue(DateProperty, value);
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

    public MultiTrackTimeline()
    {
    }
}

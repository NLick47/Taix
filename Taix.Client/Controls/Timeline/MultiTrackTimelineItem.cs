using System;
using System.Collections.Generic;

namespace Taix.Client.Controls.Timeline;

public class MultiTrackTimelineItem
{
    public string Name { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public string Color { get; set; } = "#888888";
    public string? CategoryName { get; set; }
    public string? CategoryColor { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public double Percentage { get; set; }
    public List<MultiTrackSegment> Segments { get; set; } = new();
    public object? AppModel { get; set; }

    public string DurationText
    {
        get
        {
            if (TotalDuration.TotalMinutes < 1)
                return $"{TotalDuration.Seconds}s";
            var h = (int)TotalDuration.TotalHours;
            var m = TotalDuration.Minutes;
            if (h > 0) return $"{h}h {m}m";
            return $"{m}m";
        }
    }
}

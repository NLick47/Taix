using System;

namespace Taix.Client.Controls.Timeline;

public class MultiTrackSegment
{
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public string Color { get; set; } = "#888888";
    public string? CategoryColor { get; set; }
}

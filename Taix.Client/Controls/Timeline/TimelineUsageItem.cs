using System;

namespace Taix.Client.Controls.Timeline;

public class TimelineUsageItem
{
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#888888";
    public string CategoryName { get; set; } = string.Empty;
    public string CategoryColor { get; set; } = "#888888";
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public int Duration { get; set; }
    public bool IsShortSession { get; set; }
    public bool IsIdle { get; set; }
    public object? Data { get; set; }
}

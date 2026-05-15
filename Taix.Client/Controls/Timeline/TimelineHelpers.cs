using System;
using Avalonia.Media;

namespace Taix.Client.Controls.Timeline;

internal static class TimelineHelpers
{
    internal static Color ParseColor(string? hex, Color fallback)
    {
        if (string.IsNullOrEmpty(hex)) return fallback;
        try { return Color.Parse(hex); } catch { return fallback; }
    }

    internal static bool IsIdleItem(string? name)
    {
        return string.IsNullOrEmpty(name) || name == ResourceStrings.IdleTime || name == "idle" || name == "空闲";
    }

    internal static bool IsIdleItem(TimelineUsageItem item)
    {
        return item.IsIdle || IsIdleItem(item.Name);
    }
}

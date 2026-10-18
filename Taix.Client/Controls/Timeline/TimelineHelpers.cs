using System;
using Avalonia.Media;

namespace Taix.Client.Controls.Timeline;

internal static class TimelineHelpers
{
    internal readonly record struct HighlightColors(Color Accent, Color Glow, Color Guide, Color Dim);

    internal static HighlightColors GetHighlightColors(bool isLight)
    {
        return isLight
            ? new HighlightColors(
                Accent: Color.FromArgb(0xCC, 0x2B, 0x20, 0xD9),
                Glow: Color.FromArgb(0x14, 0x2B, 0x20, 0xD9),
                Guide: Color.FromArgb(0x60, 0x2B, 0x20, 0xD9),
                Dim: Color.FromArgb(0x60, 0x00, 0x00, 0x00))
            : new HighlightColors(
                Accent: Color.FromArgb(0xCC, 0x58, 0xA6, 0xFF),
                Glow: Color.FromArgb(0x18, 0x58, 0xA6, 0xFF),
                Guide: Color.FromArgb(0x70, 0x58, 0xA6, 0xFF),
                Dim: Color.FromArgb(0x75, 0x00, 0x00, 0x00));
    }

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

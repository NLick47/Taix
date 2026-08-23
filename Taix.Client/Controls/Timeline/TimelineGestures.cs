using System;
using Avalonia;

namespace Taix.Client.Controls.Timeline;

internal static class TimelineGestures
{
    public const double WheelZoomSensitivity = 0.3;
    public const double PanRatioPerDelta = 0.15;
    public const double PanPixelsPerDelta = 40;
    public const double PanDirection = -1.0;
    public const double MaxMagnifyStep = 0.5;
    public const double MinVisibleHours = 0.25;

    public static double GetWheelZoomFactor(double delta) => Math.Exp(delta * WheelZoomSensitivity);

    public static double GetMagnifyFactor(double magnifyDelta)
        => Math.Clamp(1 + magnifyDelta, 1 - MaxMagnifyStep, 1 + MaxMagnifyStep);

    public static double GetDominantDelta(Vector delta)
        => Math.Abs(delta.X) >= Math.Abs(delta.Y) ? delta.X : delta.Y;

    public static bool IsHorizontalDominant(Vector delta)
        => Math.Abs(delta.X) >= Math.Abs(delta.Y);

    public static (double Start, double End) ZoomAt(
        double start, double end, double anchorX, double width,
        double factor, double boundStart, double boundEnd, double minSpan)
    {
        var viewStart = Math.Min(start, end);
        var viewEnd = Math.Max(start, end);
        var duration = viewEnd - viewStart;
        if (width <= 0) return (start, end);
        if (duration <= 0) duration = minSpan > 0 ? minSpan : 1;

        var boundDuration = Math.Max(boundEnd - boundStart, duration);
        var newDuration = Math.Clamp(duration / factor, minSpan, boundDuration);

        var anchorHour = viewStart + (anchorX / width) * duration;
        var newStart = anchorHour - (anchorHour - viewStart) * (newDuration / duration);
        var newEnd = newStart + newDuration;

        if (newStart < boundStart) { newStart = boundStart; newEnd = boundStart + newDuration; }
        if (newEnd > boundEnd) { newEnd = boundEnd; newStart = boundEnd - newDuration; }
        if (newStart < boundStart) { newStart = boundStart; newEnd = boundEnd; }

        return (newStart, newEnd);
    }

    public static (double Start, double End) Pan(
        double start, double end, double deltaHours, double boundStart, double boundEnd)
    {
        var viewStart = Math.Min(start, end);
        var viewEnd = Math.Max(start, end);
        var duration = viewEnd - viewStart;
        if (duration <= 0) return (start, end);

        var newStart = viewStart + deltaHours;
        var newEnd = viewEnd + deltaHours;

        if (newStart < boundStart) { newStart = boundStart; newEnd = boundStart + duration; }
        if (newEnd > boundEnd) { newEnd = boundEnd; newStart = boundEnd - duration; }
        if (newEnd < newStart) (newStart, newEnd) = (newEnd, newStart);

        return (newStart, newEnd);
    }

    public static (double Start, double End) PanByWheel(
        double start, double end, Vector delta, double boundStart, double boundEnd)
    {
        if (boundEnd - boundStart <= 0) { boundStart = 0; boundEnd = 24; }

        var viewDuration = Math.Abs(end - start);
        if (viewDuration <= 0) viewDuration = 24;

        var deltaHours = PanDirection * GetDominantDelta(delta) * viewDuration * PanRatioPerDelta;
        return Pan(start, end, deltaHours, boundStart, boundEnd);
    }
}

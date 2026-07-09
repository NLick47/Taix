using System;
using System.Collections.Generic;
using System.Linq;

namespace Taix.Client.Controls.Timeline;

public class MultiTrackTimelineItem
{
    private const int MaxDisplaySegments = 5;

    public string Name { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public string Color { get; set; } = "#888888";
    public TimeSpan TotalDuration { get; set; }
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

    public string SessionCountText => $"{Segments.Count} 次";


    public List<string> SegmentSummaries
    {
        get
        {
            var filtered = Segments
                .Where(s => s.DurationMinutes >= 2)
                .ToList();

            if (filtered.Count == 0)
                return [];

            var selected = new HashSet<int>();
            var now = DateTime.Now;

            var recentIdx = 0;
            var recentDist = double.MaxValue;
            for (var i = 0; i < filtered.Count; i++)
            {
                var dist = Math.Abs((filtered[i].End - now).TotalSeconds);
                if (dist < recentDist)
                {
                    recentDist = dist;
                    recentIdx = i;
                }
            }
            selected.Add(recentIdx);

            var byDuration = filtered
                .Select((seg, i) => (i, dur: seg.DurationMinutes * 60))
                .Where(x => !selected.Contains(x.i))
                .OrderByDescending(x => x.dur)
                .Take(2)
                .Select(x => x.i)
                .ToList();
            foreach (var idx in byDuration)
                selected.Add(idx);

            for (var i = 0; i < filtered.Count && selected.Count < MaxDisplaySegments; i++)
            {
                if (!selected.Contains(i))
                    selected.Add(i);
            }

            var items = new List<string>();
            var added = 0;
            for (var i = 0; i < filtered.Count; i++)
            {
                if (!selected.Contains(i)) continue;

                if (added >= MaxDisplaySegments)
                {
                    items.Add("...");
                    break;
                }

                var seg = filtered[i];
                var durMin = seg.DurationMinutes;
                var durText = durMin < 1
                    ? $"{(int)(seg.End - seg.Start).TotalSeconds}s"
                    : durMin >= 60
                        ? $"{durMin / 60}h{durMin % 60}m"
                        : $"{durMin}m";

                items.Add($"{seg.Start:HH:mm}-{seg.End:HH:mm}  {durText}");
                added++;
            }

            return items;
        }
    }
}

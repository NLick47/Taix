using System.Collections.Concurrent;
using System.Threading;

namespace Taix.Client.Base.Color;

public static class TimelineColorService
{
    private static readonly ConcurrentDictionary<string, int> _assigned = new();
    private static int _nextSeed;

    public static string GetColor(string? appName, bool isDark)
    {
        var palette = isDark ? Colors.TimelinePaletteDark : Colors.TimelinePaletteLight;
        if (string.IsNullOrEmpty(appName)) return palette[0];

        var idx = _assigned.GetOrAdd(appName, _ =>
        {
            var hash = Fnv1AHash(appName);
            var used = _assigned.Values;
            for (var i = 0; i < palette.Length; i++)
            {
                var candidate = (hash + i) % palette.Length;
                if (!used.Contains(candidate))
                    return candidate;
            }
            return Interlocked.Increment(ref _nextSeed) % palette.Length;
        });
        return palette[idx];
    }

    public static void Reset()
    {
        _assigned.Clear();
        _nextSeed = 0;
    }

    private static int Fnv1AHash(string key)
    {
        unchecked
        {
            var hash = (int)2166136261;
            foreach (var c in key)
            {
                hash ^= c;
                hash *= 16777619;
            }
            return hash & 0x7FFFFFFF;
        }
    }
}

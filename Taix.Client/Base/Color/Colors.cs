using System.Collections.Generic;
using Avalonia.Media;

namespace Taix.Client.Base.Color;

public class Colors
{
    public static Dictionary<ColorTypes, IColor> ColorList = new()
    {
        { ColorTypes.Aquamarine, new IColor { Name = "碧绿", Color = "#00AA90" } },
        { ColorTypes.Black, new IColor { Name = "暗黑", Color = "#080808" } },
        { ColorTypes.Blue, new IColor { Name = "蓝色", Color = "#0078d4" } },
        { ColorTypes.Cyan, new IColor { Name = "青色", Color = "#51A8DD" } },
        { ColorTypes.Gold, new IColor { Name = "金色", Color = "#EFBB24" } },
        { ColorTypes.Gray, new IColor { Name = "灰色", Color = "#828282" } },
        { ColorTypes.Green, new IColor { Name = "绿色", Color = "#56C773" } },
        { ColorTypes.Orange, new IColor { Name = "橙色", Color = "#E98B2A" } },
        { ColorTypes.Pink, new IColor { Name = "粉红", Color = "#B5495B" } },
        { ColorTypes.Red, new IColor { Name = "赤红", Color = "#F3221B" } },
        { ColorTypes.Violet, new IColor { Name = "紫色", Color = "#77428D" } },
        { ColorTypes.Yellow, new IColor { Name = "黄色", Color = "#FFC408" } },
        { ColorTypes.White, new IColor { Name = "白色", Color = "#FFFFFF" } }
    };

    public static string[] MainColors =
    {
        "#00FFAB", "#A761FF", "#ff587f", "#fff323", "#DAEAF1", "#2B20D9", "#FF5C01", "#9EB23C", "#FF9999", "#998CEB",
        "#77E4D4", "#FD5E5E", "#B4FF9F", "#07FF01", "#FFE162", "#C70B80", "#590696", "#97DBAE", "#ff1801"
    };

    public static string[] TimelinePaletteDark { get; } = GenerateTimelinePalette(45, isDark: true);

    public static string[] TimelinePaletteLight { get; } = GenerateTimelinePalette(45, isDark: false);

    // 颜色缓存，避免重复计算 hash
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<(string Key, bool IsDark), string> _timelineColorCache = new();

    public static string GetTimelinePaletteColor(string key, bool isDark)
    {
        if (string.IsNullOrEmpty(key)) return isDark ? TimelinePaletteDark[0] : TimelinePaletteLight[0];
        return _timelineColorCache.GetOrAdd((key, isDark), static k =>
        {
            var palette = k.IsDark ? TimelinePaletteDark : TimelinePaletteLight;
            var hash = k.Key.GetHashCode();
            var index = System.Math.Abs(hash) % palette.Length;
            return palette[index];
        });
    }

    private static string[] GenerateTimelinePalette(int count, bool isDark)
    {
        var baseL = isDark ? 0.60 : 0.45;
        var saturation = isDark ? 0.80 : 0.72;
        var result = new string[count];

        for (var i = 0; i < count; i++)
        {
            var h = i * 360.0 / count;
            var l = h switch
            {
                >= 36 and <= 72 => baseL + 0.10,
                >= 200 and <= 260 => baseL - 0.05,
                >= 270 and <= 310 => baseL + 0.03,
                _ => baseL
            };
            if (l < 0.25) l = 0.25;
            if (l > 0.85) l = 0.85;
            result[i] = HslToHex(h, saturation, l);
        }

        return result;
    }

    private static string HslToHex(double h, double s, double l)
    {
        var c = (1.0 - System.Math.Abs(2.0 * l - 1.0)) * s;
        var x = c * (1.0 - System.Math.Abs((h / 60.0) % 2.0 - 1.0));
        var m = l - c / 2.0;

        double r, g, b;
        if (h < 60) { r = c; g = x; b = 0; }
        else if (h < 120) { r = x; g = c; b = 0; }
        else if (h < 180) { r = 0; g = c; b = x; }
        else if (h < 240) { r = 0; g = x; b = c; }
        else if (h < 300) { r = x; g = 0; b = c; }
        else { r = c; g = 0; b = x; }

        return $"#{(byte)((r + m) * 255):X2}{(byte)((g + m) * 255):X2}{(byte)((b + m) * 255):X2}";
    }

    public static IColor Get(ColorTypes color)
    {
        return ColorList[color];
    }

    public static SolidColorBrush GetColor(ColorTypes color, double opacity = 1)
    {
        return GetFromString(ColorList[color].Color, opacity);
    }

    public static SolidColorBrush GetFromString(string color, double opacity = 1)
    {
        if (string.IsNullOrEmpty(color)) color = StateData.ThemeColor;

        return new SolidColorBrush(Avalonia.Media.Color.Parse(color), opacity);
    }

    public struct IColor
    {
        /// <summary>
        /// 颜色名称
        /// </summary>
        public string Name;

        /// <summary>
        /// 颜色
        /// </summary>
        public string Color;
    }
}

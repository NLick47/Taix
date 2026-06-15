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

    public static string[] TimelinePaletteDark =
    {
        "#5b8ff9", "#5ad8a6", "#f6bd16", "#e8684a", "#6dc8ec",
        "#9270ca", "#ff9d4d", "#269a99", "#ff99c3", "#a0d911",
        "#13c2c2", "#eb2f96", "#faad14", "#fadb14", "#a8071a"
    };

    public static string[] TimelinePaletteLight =
    {
        "#5470c6", "#91cc75", "#fac858", "#ee6666", "#73c0de",
        "#3ba272", "#fc8452", "#9a60b4", "#ea7ccc", "#37a2da",
        "#32c5e9", "#9fe6b8", "#ffdb5c", "#ff9f7f", "#fb7293"
    };

    public static string GetTimelinePaletteColor(string key, bool isDark)
    {
        var palette = isDark ? TimelinePaletteDark : TimelinePaletteLight;
        if (string.IsNullOrEmpty(key)) return palette[0];
        var hash = key.GetHashCode();
        var index = System.Math.Abs(hash) % palette.Length;
        return palette[index];
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
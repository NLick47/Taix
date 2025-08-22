using System.Collections.Generic;
using SharedLibrary;

namespace UI.Controls.Charts.Model;

/// <summary>
///     徽章模型
/// </summary>
public class ChartBadgeModel
{
    /// <summary>
    ///     忽略应用徽章
    /// </summary>
    public static ChartBadgeModel IgnoreBadge = new()
    {
        Name = ResourceStrings.Ignore,
        Color = "#f51837",
        Type = ChartBadgeType.Ignore
    };

    public static IReadOnlySet<string> IgnreLanguages = new HashSet<string>
    {
        "忽略",
        "ignore"
    };

    /// <summary>
    ///     徽章名称
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    ///     徽章颜色
    /// </summary>
    public string Color { get; set; }

    /// <summary>
    ///     徽章类型
    /// </summary>
    public ChartBadgeType Type { get; set; }
}
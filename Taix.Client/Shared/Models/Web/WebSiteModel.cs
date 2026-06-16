namespace Taix.Client.Shared.Models.Web;

/// <summary>
/// 网页站点数据库模型
/// </summary>
public record class WebSiteModel
{
    public int ID { get; init; }

    /// <summary>
    /// 标题
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// 域名
    /// </summary>
    public string? Domain { get; init; }

    /// <summary>
    /// 别名
    /// </summary>
    public string? Alias { get; init; }

    /// <summary>
    /// 分类ID
    /// </summary>
    public int CategoryID { get; init; }

    /// <summary>
    /// 图标路径
    /// </summary>
    public string? IconFile { get; init; }

    /// <summary>
    /// 累计使用时长
    /// </summary>
    public int Duration { get; init; }

    public WebSiteCategoryModel Category { get; init; }
}

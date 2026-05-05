namespace Taix.Client.Shared.Models.Db;

/// <summary>
/// 网站分类数据库模型
/// </summary>
public record class WebSiteCategoryModel
{
    public int ID { get; init; }

    /// <summary>
    /// 分类名称
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// 分类图标路径
    /// </summary>
    public string? IconFile { get; init; }

    /// <summary>
    /// 颜色
    /// </summary>
    public string? Color { get; init; }

    /// <summary>
    /// 是否为系统分类（不可删除）
    /// </summary>
    public bool IsSystem { get; init; }


}

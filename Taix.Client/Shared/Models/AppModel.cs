namespace Taix.Client.Shared.Models;

/// <summary>
/// 应用信息模型
/// </summary>
public record class AppModel
{
    public int ID { get; init; }

    /// <summary>
    /// 名称
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// 别名
    /// </summary>
    public string? Alias { get; init; }

    /// <summary>
    /// 描述
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// 文件
    /// </summary>
    public string? File { get; init; }

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
    public int TotalTime { get; init; }

    public CategoryModel? Category { get; init; }
}

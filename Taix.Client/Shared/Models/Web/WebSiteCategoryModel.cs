namespace Taix.Client.Shared.Models.Db;

/// <summary>
///     网站分类数据库模型
/// </summary>
public class WebSiteCategoryModel
{
    public int ID { get; set; }

    /// <summary>
    ///     分类名称
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    ///     分类图标路径
    /// </summary>
    public string? IconFile { get; set; }

    /// <summary>
    ///     颜色
    /// </summary>
    public string? Color { get; set; }

    /// <summary>
    ///     是否为系统分类（不可删除）
    /// </summary>
    public bool IsSystem { get; set; }

    public static WebSiteCategoryModel DefaultSystemCategory()
    {
        return new WebSiteCategoryModel
        {
            Name = "未分类",
            IconFile = "avares://Taix/Resources/Icons/tai.ico",
            Color = "#E5F7F6F2",
            IsSystem = true
        };
    }
}

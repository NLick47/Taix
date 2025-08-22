using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Core.Models;

/// <summary>
///     应用信息模型
/// </summary>
[Table("AppModels")]
public class AppModel
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int ID { get; set; }

    /// <summary>
    ///     名称
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    ///     别名
    /// </summary>
    public string? Alias { get; set; }

    /// <summary>
    ///     描述
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    ///     文件
    /// </summary>
    public string? File { get; set; }

    /// <summary>
    ///     分类ID
    /// </summary>
    public int CategoryID { get; set; }

    /// <summary>
    ///     图标路径
    /// </summary>
    public string? IconFile { get; set; }

    /// <summary>
    ///     累计使用时长
    /// </summary>
    public int TotalTime { get; set; }

    [ForeignKey("CategoryID")] public virtual CategoryModel Category { get; set; }
}
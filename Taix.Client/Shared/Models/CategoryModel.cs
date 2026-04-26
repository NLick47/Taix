using System.Collections.Generic;
using System.Text.Json;
using Taix.Client.Shared.Librarys;

namespace Taix.Client.Shared.Models;


public class CategoryModel
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
    ///     是否启用目录匹配
    /// </summary>
    public bool IsDirectoryMath { get; set; }

    /// <summary>
    ///     是否为系统分类（不可删除）
    /// </summary>
    public bool IsSystem { get; set; }

    /// <summary>
    ///     匹配目录（Json List string）
    /// </summary>
    public string? Directories { get; set; }

    /// <summary>
    ///     匹配目录集合（已解析）
    /// </summary>
    public List<string> DirectoryList
    {
        get
        {
            if (string.IsNullOrEmpty(Directories)) return new List<string>();
            return JsonSerializer.Deserialize<List<string>>(Directories, ClientJsonContext.Default.ListString);
        }
    }

    public static CategoryModel DefaultSystemCategory()
    {
        return new CategoryModel
        {
            Name = "未分类",
            IconFile = "avares://Taix/Resources/Icons/tai.ico",
            Color = "#E5F7F6F2",
            IsDirectoryMath = false,
            IsSystem = true
        };
    }
}

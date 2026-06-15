using System.Collections.Generic;
using System.Text.Json;
using Taix.Client.Shared.Librarys;

namespace Taix.Client.Shared.Models;

public record class CategoryModel
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
    /// 是否启用目录匹配
    /// </summary>
    public bool IsDirectoryMatch { get; init; }

    /// <summary>
    /// 是否为系统分类（不可删除）
    /// </summary>
    public bool IsSystem { get; init; }

    /// <summary>
    /// 匹配目录（Json List string）
    /// </summary>
    public string? Directories { get; init; }

    /// <summary>
    /// 匹配目录集合（已解析）
    /// </summary>
    public List<string> DirectoryList
    {
        get
        {
            if (string.IsNullOrEmpty(Directories)) return new List<string>();
            return JsonSerializer.Deserialize<List<string>>(Directories, ClientJsonContext.Default.ListString);
        }
    }


}

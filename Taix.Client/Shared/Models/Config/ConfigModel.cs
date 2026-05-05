using System.Collections.Generic;
using Taix.Client.Shared.Models.Config.Link;

namespace Taix.Client.Shared.Models.Config;

/// <summary>
/// 应用设置
/// </summary>
public class ConfigModel
{
    public const int CurrentVersion = 1;

    /// <summary>
    /// 配置版本
    /// </summary>
    public int Version { get; set; }

    /// <summary>
    /// 关联进程
    /// </summary>
    public List<LinkModel> Links { get; set; } = new();

    /// <summary>
    /// 常规
    /// </summary>
    public GeneralModel General { get; set; } = new();

    /// <summary>
    /// 行为
    /// </summary>
    public BehaviorModel Behavior { get; set; } = new();

}

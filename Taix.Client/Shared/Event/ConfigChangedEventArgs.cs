using System;
using System.Collections.Generic;
using System.Linq;
using Taix.Client.Shared.Models.Config;

namespace Taix.Client.Shared.Event;

/// <summary>
/// 配置变更事件参数，携带变更路径信息，便于消费者精确响应特定配置项变更。
/// </summary>
public class ConfigChangedEventArgs : EventArgs
{
    /// <summary>
    /// 变更前的配置
    /// </summary>
    public ConfigModel OldConfig { get; }

    /// <summary>
    /// 变更后的配置
    /// </summary>
    public ConfigModel NewConfig { get; }

    /// <summary>
    /// 发生变更的属性路径列表，例如 "General.Language"、"General.Theme"
    /// </summary>
    public IReadOnlyList<string> ChangedPaths { get; }

    public ConfigChangedEventArgs(ConfigModel oldConfig, ConfigModel newConfig, IReadOnlyList<string> changedPaths)
    {
        OldConfig = oldConfig;
        NewConfig = newConfig;
        ChangedPaths = changedPaths;
    }

    /// <summary>
    /// 检查指定路径是否发生变更
    /// </summary>
    public bool HasChange(string path)
    {
        return ChangedPaths.Contains(path);
    }

    /// <summary>
    /// 检查任意一条路径是否发生变更
    /// </summary>
    public bool HasAnyChange(params string[] paths)
    {
        return paths.Any(ChangedPaths.Contains);
    }
}

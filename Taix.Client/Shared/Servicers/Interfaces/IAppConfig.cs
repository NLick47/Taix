using System.Threading.Tasks;
using Taix.Client.Shared.Event;
using Taix.Client.Shared.Models.Config;

namespace Taix.Client.Shared.Servicers.Interfaces;

/// <summary>
///     应用配置
/// </summary>
public interface IAppConfig
{
    /// <summary>
    ///     加载配置
    /// </summary>
    Task LoadAsync();

    /// <summary>
    ///     获取配置
    /// </summary>
    /// <returns></returns>
    ConfigModel GetConfig();

    /// <summary>
    ///     更新配置
    /// </summary>
    Task SaveAsync();

    /// <summary>
    ///     配置修改时发生
    /// </summary>
    event AppConfigEventHandler ConfigChanged;
}

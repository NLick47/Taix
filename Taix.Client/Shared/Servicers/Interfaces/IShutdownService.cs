using System;
using System.Threading.Tasks;

namespace Taix.Client.Shared.Servicers.Interfaces;

/// <summary>
/// 应用退出管理
/// </summary>
public interface IShutdownService
{
    /// <summary>
    /// 注册退出时的处理任务，按注册顺序依次 await
    /// </summary>
    void AddHandler(Func<Task> handler);

    /// <summary>
    /// 触发所有已注册的退出处理，全部完成后返回
    /// </summary>
    Task ShutdownAsync();
}

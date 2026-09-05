using System.Threading.Tasks;
using Taix.Client.Shared.Models.Config;

namespace Taix.Client.Shared.Servicers.Interfaces;

/// <summary>
/// 窗口几何的持久化。只负责"存什么、读什么"，不关心窗口本身。
/// </summary>
public interface IWindowStateService
{
    /// <summary>上次保存的快照，没有历史数据时为 null。</summary>
    WindowSnapshot? Last { get; }

    Task LoadAsync();

    Task SaveAsync(WindowSnapshot snapshot);
}

using System.Threading.Tasks;
using Avalonia.Controls;

namespace Taix.Client.Shared.Servicers.Interfaces;

public interface IShortcutService
{
    void Attach(Window window);
    void Detach();

    Task TriggerRefreshAsync();
    Task TriggerSearchAsync();
    Task TriggerNavigateBackAsync();
}

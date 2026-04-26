using Avalonia.Controls;

namespace Taix.Client.Servicers;

public interface IAppContextMenuServicer
{
    void Init();
    ContextMenu GetContextMenu();
}
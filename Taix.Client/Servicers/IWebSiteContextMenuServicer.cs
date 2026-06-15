using Avalonia.Controls;

namespace Taix.Client.Servicers;

public interface IWebSiteContextMenuServicer
{
    void Init();
    ContextMenu GetContextMenu();
}
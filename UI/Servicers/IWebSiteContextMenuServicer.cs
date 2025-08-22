using Avalonia.Controls;

namespace UI.Servicers;

public interface IWebSiteContextMenuServicer
{
    void Init();
    ContextMenu GetContextMenu();
}
using Avalonia.Controls;

namespace UI.Servicers;

public interface IAppContextMenuServicer
{
    void Init();
    ContextMenu GetContextMenu();
}
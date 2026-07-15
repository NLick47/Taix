using System.Threading.Tasks;
using Avalonia.Controls;
using Taix.Client.Controls.Charts;
using Taix.Client.Controls.Charts.Model;

namespace Taix.Client.Servicers;

public interface IContextMenuServicer
{
    void Init();
    Task<ContextMenu> CreateContextMenuAsync(ContextMenuType type, ChartsDataModel data);
}

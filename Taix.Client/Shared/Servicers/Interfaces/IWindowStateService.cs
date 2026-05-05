using System.Threading.Tasks;

namespace Taix.Client.Shared.Servicers.Interfaces;

public interface IWindowStateService
{
    double WindowWidth { get; set; }
    double WindowHeight { get; set; }

    Task LoadAsync();
    Task SaveAsync();
}

using System.Threading.Tasks;

namespace Taix.Client.Servicers.Interfaces;

public interface IClipboardService
{
    Task SetTextAsync(string text);
    Task<string?> GetTextAsync();
}

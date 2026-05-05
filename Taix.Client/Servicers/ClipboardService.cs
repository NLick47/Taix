using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Taix.Client.Servicers.Interfaces;

namespace Taix.Client.Servicers;

public class ClipboardService : IClipboardService
{
    public async Task SetTextAsync(string text)
    {
        var clipboard = GetClipboard();
        if (clipboard != null)
        {
            var item = new DataTransferItem();
            item.Set(DataFormat.Text, text);
            var data = new DataTransfer();
            data.Add(item);
            await clipboard.SetDataAsync(data);
        }
    }

    public async Task<string?> GetTextAsync()
    {
        var clipboard = GetClipboard();
        if (clipboard != null)
        {
            var data = await clipboard.TryGetDataAsync();
            if (data != null)
                return await data.TryGetTextAsync();
        }
        return null;
    }

    private static Avalonia.Input.Platform.IClipboard? GetClipboard()
    {
        var desktop = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        return desktop?.MainWindow?.Clipboard;
    }
}

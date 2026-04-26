using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Taix.Client.Servicers.Interfaces;

namespace Taix.Client.Servicers;

public class ClipboardService : IClipboardService
{
    public Task SetTextAsync(string text)
    {
        var clipboard = GetClipboard();
        if (clipboard != null)
            return clipboard.SetTextAsync(text);
        return Task.CompletedTask;
    }

    public Task<string?> GetTextAsync()
    {
        var clipboard = GetClipboard();
        if (clipboard != null)
            return clipboard.GetTextAsync();
        return Task.FromResult<string?>(null);
    }

    private static Avalonia.Input.Platform.IClipboard? GetClipboard()
    {
        var desktop = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        return desktop?.MainWindow?.Clipboard;
    }
}

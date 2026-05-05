using System;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using Taix.Client.Servicers.Interfaces;
using Taix.Client.Views;

namespace Taix.Client.Servicers;

public class UIServicer : IUIServicer, IDialogService
{
    private readonly MainWindow _window;

    public UIServicer(MainWindow window)
    {
        _window = window;
    }

    public Task<bool> ShowConfirmDialogAsync(string title, string message)
    {
        return _window.ShowConfirmDialogAsync(title, message);
    }

    public Task<string?> ShowInputModalAsync(string title, string placeholder, string value = null, Func<string, bool>? validate = null)
    {
        return _window.ShowInputModalAsync(title, placeholder, value ?? string.Empty, validate);
    }

    public Task<int> ShowActionDialogAsync(string title, string message, string[] buttons)
    {
        return _window.ShowActionDialogAsync(title, message, buttons);
    }

    public async Task<string?> ShowFolderPickerAsync(string? title = null)
    {
        var storage = _window.StorageProvider;
        var result = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            AllowMultiple = false,
            Title = title
        });
        return result?.Count > 0 ? result[0].Path.LocalPath : null;
    }
}
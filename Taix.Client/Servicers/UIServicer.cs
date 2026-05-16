using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Taix.Client.Servicers.Interfaces;
using Taix.Client.Views;

namespace Taix.Client.Servicers;

public class UIServicer : IUIServicer, IDialogService
{
    private static MainWindow? GetMainWindow()
    {
        var desk = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        return desk?.MainWindow as MainWindow;
    }

    public Task<bool> ShowConfirmDialogAsync(string title, string message)
    {
        var window = GetMainWindow();
        if (window == null) throw new InvalidOperationException("MainWindow is not available.");
        return window.ShowConfirmDialogAsync(title, message);
    }

    public Task<string?> ShowInputModalAsync(string title, string placeholder, string value = null, Func<string, bool>? validate = null)
    {
        var window = GetMainWindow();
        if (window == null) throw new InvalidOperationException("MainWindow is not available.");
        return window.ShowInputModalAsync(title, placeholder, value ?? string.Empty, validate);
    }

    public Task<int> ShowActionDialogAsync(string title, string message, string[] buttons)
    {
        var window = GetMainWindow();
        if (window == null) throw new InvalidOperationException("MainWindow is not available.");
        return window.ShowActionDialogAsync(title, message, buttons);
    }

    public async Task<string?> ShowFolderPickerAsync(string? title = null)
    {
        var window = GetMainWindow();
        if (window == null) throw new InvalidOperationException("MainWindow is not available.");

        var storage = window.StorageProvider;
        var result = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            AllowMultiple = false,
            Title = title
        });
        return result?.Count > 0 ? result[0].Path.LocalPath : null;
    }
}
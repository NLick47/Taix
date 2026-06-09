using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Taix.Client.Controls.Window;
using Taix.Client.Servicers.Interfaces;
using Taix.Client.ViewModels;
using Taix.Client.Views;

namespace Taix.Client.Servicers;

public class UIServicer : IUIServicer, IDialogService
{
    private static MainWindow? GetMainWindow()
    {
        var desk = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        return desk?.MainWindow as MainWindow;
    }

    private static MainViewModel? GetMainViewModel()
    {
        return ServiceLocator.GetService<MainViewModel>();
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

    public async Task<CategoryResult?> ShowCreateCategoryDialogAsync(
        string title,
        string defaultName = null,
        IEnumerable<string>? existingNames = null)
    {
        const string defaultIcon = "avares://Taix/Resources/Emoji/(1).png";
        const string defaultColor = "#00FFAB";
        const int maxNameLength = 10;

        var existingNameSet = existingNames?.ToHashSet(StringComparer.OrdinalIgnoreCase) ?? new HashSet<string>();

        try
        {
            var name = await ShowInputModalAsync(
                title,
                ResourceStrings.EnterCategoryName ?? "请输入分类名称",
                defaultName ?? string.Empty,
                n => ValidateCategoryName(n, maxNameLength, existingNameSet));

            if (string.IsNullOrWhiteSpace(name))
                return null;

            return new CategoryResult(name, defaultIcon, defaultColor);
        }
        catch (Exception ex) when (ex.Message == "Input cancel")
        {
            // 用户取消输入，返回 null
            return null;
        }
    }

    private bool ValidateCategoryName(string name, int maxLength, HashSet<string> existingNames)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        if (name.Length > maxLength)
            return false;

        if (existingNames.Contains(name))
        {
            // 显示错误提示
            GetMainViewModel()?.Error(ResourceStrings.CategoryNameExists);
            return false;
        }

        return true;
    }
}
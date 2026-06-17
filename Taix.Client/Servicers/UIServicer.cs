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
        IEnumerable<string>? existingNames = null,
        IEnumerable<string>? existingColors = null)
    {
        const string defaultIcon = "avares://Taix/Resources/Emoji/(1).png";
        const int maxNameLength = 10;

        var existingNameSet = existingNames?.ToHashSet(StringComparer.OrdinalIgnoreCase) ?? new HashSet<string>();
        var existingColorSet = existingColors?.ToHashSet(StringComparer.OrdinalIgnoreCase) ?? new HashSet<string>();

        // 生成一个不重复的默认颜色
        var defaultColor = GenerateUniqueColor(existingColorSet);

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

    private static string GenerateUniqueColor(HashSet<string> existingColors)
    {
        // 预定义的颜色列表
        var predefinedColors = new[]
        {
            "#00FFAB", "#FF6B6B", "#4ECDC4", "#FFE66D", "#95E1D3",
            "#F38181", "#AA96DA", "#FCBAD3", "#A8D8EA", "#FFB6B9",
            "#61C0BF", "#BBDED6", "#FAE3D9", "#E8E8E8", "#FF9A8B",
            "#88D8B0", "#FFAAA5", "#FFD3B6", "#D4E09B", "#F6F0ED"
        };

        // 找一个未被使用的颜色
        foreach (var color in predefinedColors)
        {
            if (!existingColors.Contains(color))
                return color;
        }

        // 如果预定义颜色都用完了，生成一个随机颜色
        var random = new Random();
        string newColor;
        do
        {
            var r = random.Next(64, 256);
            var g = random.Next(64, 256);
            var b = random.Next(64, 256);
            newColor = $"#{r:X2}{g:X2}{b:X2}";
        } while (existingColors.Contains(newColor));

        return newColor;
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
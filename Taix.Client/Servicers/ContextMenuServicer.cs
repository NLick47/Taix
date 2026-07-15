using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.VisualTree;
using Taix.Client.Controls.Base;
using Taix.Client.Controls.Charts;
using Taix.Client.Controls.Charts.Model;
using Taix.Client.Controls.Window;
using Taix.Client.Events;
using Taix.Client.Logging;
using Taix.Client.Platform;
using Taix.Client.Servicers.Interfaces;
using Taix.Client.Shared.Helpers;
using Taix.Client.Shared.Models;
using Taix.Client.Shared.Models.Web;
using Taix.Client.Shared.Servicers.Interfaces;
using Taix.Client.ViewModels;
using Taix.Client.Views;

namespace Taix.Client.Servicers;

public class ContextMenuServicer : IContextMenuServicer, IDisposable
{
    private readonly ICategorys _categorys;
    private readonly IAppData _appData;
    private readonly IAppConfig _appConfig;
    private readonly IUIServicer _uiServicer;
    private readonly IClipboardService _clipboardService;
    private readonly IData _dataService;
    private readonly IWebData _webData;
    private readonly IAppUpdateService _appUpdateService;
    private readonly IAppEventService _appEventService;
    private MainViewModel? _mainViewModel;

    public ContextMenuServicer(
        ICategorys categorys,
        IAppData appData,
        IAppConfig appConfig,
        IUIServicer uiServicer,
        IClipboardService clipboardService,
        IData dataService,
        IWebData webData,
        IAppUpdateService appUpdateService,
        IAppEventService appEventService)
    {
        _categorys = categorys;
        _appData = appData;
        _appConfig = appConfig;
        _uiServicer = uiServicer;
        _clipboardService = clipboardService;
        _dataService = dataService;
        _webData = webData;
        _appUpdateService = appUpdateService;
        _appEventService = appEventService;
    }

    public void Init()
    {
        if (GetMainWindow() is { } mainWindow)
            mainWindow.PointerPressed += OnGlobalPointerPressed;

        _mainViewModel = ServiceLocator.GetService<MainViewModel>();
    }

    public void Dispose()
    {
        if (GetMainWindow() is { } mainWindow)
            mainWindow.PointerPressed -= OnGlobalPointerPressed;
    }

    private static MainWindow? GetMainWindow() =>
        (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow as MainWindow;

    public async Task<ContextMenu> CreateContextMenuAsync(ContextMenuType type, ChartsDataModel data)
    {
        var app = GetApp(data);
        var site = GetSite(data);
        return type switch
        {
            ContextMenuType.App => await BuildAppMenuAsync(data),
            ContextMenuType.WebSite => await BuildWebSiteMenuAsync(data),
            ContextMenuType.AppDetail => await BuildAppDetailMenuAsync(data),
            ContextMenuType.WebSiteDetail => await BuildWebSiteDetailMenuAsync(data),
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
    }

    #region App Menu

    private async Task<ContextMenu> BuildAppMenuAsync(ChartsDataModel data)
    {
        var app = GetApp(data);
        var menu = new ContextMenu();

        var blockItem = new MenuItem { Header = ResourceStrings.IgnoreThisApplication };
        blockItem.Click += (_, _) => OnBlockApp(data);
        var whiteListItem = new MenuItem { Header = ResourceStrings.AddWhitelist };
        whiteListItem.Click += (_, _) => OnWhiteListApp(data);
        var setCategoryItem = new MenuItem { Header = ResourceStrings.SetCategory };

        if (app != null)
            await PopulateAppCategorySubmenuAsync(setCategoryItem, app.ID);

        menu.Opening += (_, _) => UpdateBlockWhiteListState(data, blockItem, whiteListItem);

        menu.Items.Add(CreateMenuItem(ResourceStrings.StartApplication, () => RunApp(data)));
        menu.Items.Add(new Separator());
        menu.Items.Add(setCategoryItem);
        menu.Items.Add(CreateMenuItem(ResourceStrings.EditAlias, () => EditAppAliasAsync(data)));
        var openDirItem = CreateMenuItem(ResourceStrings.OpenApplicationDirectory, () => OpenAppDir(data));
        openDirItem.IsVisible = !PlatformHelper.IsMacOS;
        menu.Items.Add(openDirItem);
        menu.Items.Add(new Separator());
        menu.Items.Add(blockItem);
        menu.Items.Add(whiteListItem);

        return menu;
    }

    private void UpdateBlockWhiteListState(ChartsDataModel data, MenuItem blockItem, MenuItem whiteListItem)
    {
        var app = GetApp(data);
        if (app == null) return;

        var config = _appConfig.GetConfig();
        blockItem.Header = config.Behavior.IgnoreProcessList.Contains(app.Name)
            ? ResourceStrings.Unignore
            : ResourceStrings.IgnoreThisApplication;
        whiteListItem.Header = config.Behavior.ProcessWhiteList.Contains(app.Name)
            ? ResourceStrings.RemoveWhitelist
            : ResourceStrings.AddWhitelist;
    }

    private ContextMenu BuildAppDetailMenu(ChartsDataModel data)
    {
        var app = GetApp(data);
        var menu = new ContextMenu();

        var blockItem = new MenuItem { Header = ResourceStrings.IgnoreThisApplication };
        blockItem.Click += (_, _) => OnBlockApp(data);
        var whiteListItem = new MenuItem { Header = ResourceStrings.AddWhitelist };
        whiteListItem.Click += (_, _) => OnWhiteListApp(data);
        var setCategoryItem = new MenuItem { Header = ResourceStrings.SetCategory };

        menu.Opening += (_, _) => UpdateBlockWhiteListState(data, blockItem, whiteListItem);

        menu.Items.Add(CreateMenuItem(ResourceStrings.StartApplication, () => RunApp(data)));
        menu.Items.Add(new Separator());
        menu.Items.Add(CreateMenuItem(ResourceStrings.Refresh, RefreshCurrentPage));
        menu.Items.Add(new Separator());
        menu.Items.Add(setCategoryItem);
        menu.Items.Add(CreateMenuItem(ResourceStrings.EditAlias, () => EditAppAliasAsync(data)));
        menu.Items.Add(new Separator());
        menu.Items.Add(CreateMenuItem(ResourceStrings.CopyApplicationProcessName, () => CopyAppName(data)));
        menu.Items.Add(CreateMenuItem(ResourceStrings.CopyApplicationFilePath, () => CopyAppFile(data)));
        var openDirItem = CreateMenuItem(ResourceStrings.OpenApplicationDirectory, () => OpenAppDir(data));
        openDirItem.IsVisible = !PlatformHelper.IsMacOS;
        menu.Items.Add(openDirItem);
        menu.Items.Add(new Separator());
        menu.Items.Add(CreateMenuItem(ResourceStrings.ClearStatistics, () => ClearAppDataAsync(data)));
        menu.Items.Add(blockItem);
        menu.Items.Add(whiteListItem);

        return menu;
    }

    private async Task<ContextMenu> BuildAppDetailMenuAsync(ChartsDataModel data)
    {
        var app = GetApp(data);
        var menu = new ContextMenu();

        var blockItem = new MenuItem { Header = ResourceStrings.IgnoreThisApplication };
        blockItem.Click += (_, _) => OnBlockApp(data);
        var whiteListItem = new MenuItem { Header = ResourceStrings.AddWhitelist };
        whiteListItem.Click += (_, _) => OnWhiteListApp(data);
        var setCategoryItem = new MenuItem { Header = ResourceStrings.SetCategory };

        if (app != null)
            await PopulateAppCategorySubmenuAsync(setCategoryItem, app.ID);

        menu.Opening += (_, _) => UpdateBlockWhiteListState(data, blockItem, whiteListItem);

        menu.Items.Add(CreateMenuItem(ResourceStrings.StartApplication, () => RunApp(data)));
        menu.Items.Add(new Separator());
        menu.Items.Add(CreateMenuItem(ResourceStrings.Refresh, RefreshCurrentPage));
        menu.Items.Add(new Separator());
        menu.Items.Add(setCategoryItem);
        menu.Items.Add(CreateMenuItem(ResourceStrings.EditAlias, () => EditAppAliasAsync(data)));
        menu.Items.Add(new Separator());
        menu.Items.Add(CreateMenuItem(ResourceStrings.CopyApplicationProcessName, () => CopyAppName(data)));
        menu.Items.Add(CreateMenuItem(ResourceStrings.CopyApplicationFilePath, () => CopyAppFile(data)));
        var openDirItem = CreateMenuItem(ResourceStrings.OpenApplicationDirectory, () => OpenAppDir(data));
        openDirItem.IsVisible = !PlatformHelper.IsMacOS;
        menu.Items.Add(openDirItem);
        menu.Items.Add(new Separator());
        menu.Items.Add(CreateMenuItem(ResourceStrings.ClearStatistics, () => ClearAppDataAsync(data)));
        menu.Items.Add(blockItem);
        menu.Items.Add(whiteListItem);

        return menu;
    }

    private static AppModel? GetApp(ChartsDataModel data)
    {
        var result = data?.Data switch
        {
            DailyLogModel dailyLog => dailyLog.AppModel,
            HoursLogModel hoursLog => hoursLog.AppModel,
            AppModel app => app,
            _ => null
        };
        Logger.Debug($"[ContextMenuServicer] GetApp: data.Data type={data?.Data?.GetType().Name ?? "null"}, result={result?.Name ?? "null"}");
        return result;
    }

    private void RunApp(ChartsDataModel data)
    {
        var app = GetApp(data);
        if (app == null) return;

        if (File.Exists(app.File))
        {
            PlatformHelper.RunFile(app.File);
            _mainViewModel?.Toast(ResourceStrings.OperationCompleted);
        }
        else
        {
            _mainViewModel?.Toast(ResourceStrings.ApplicationFileExist, ToastType.Error, IconTypes.IncidentTriangle);
        }
    }

    private void OpenAppDir(ChartsDataModel data)
    {
        var app = GetApp(data);
        if (app == null) return;

        if (File.Exists(app.File))
            PlatformHelper.OpenFileInExplorer(app.File);
        else
            _mainViewModel?.Toast(ResourceStrings.ApplicationFileExist, ToastType.Error, IconTypes.IncidentTriangle);
    }

    private async void EditAppAliasAsync(ChartsDataModel data)
    {
        var app = GetApp(data);
        if (app == null) return;

        var input = await _uiServicer.ShowInputModalAsync(
            ResourceStrings.UpdateAlias,
            ResourceStrings.EnterAlias,
            app.Alias,
            ValidateAliasLength);

        if (input == null) return;

        await _appUpdateService.UpdateAliasAsync(app.ID, input);
        _mainViewModel?.Success(ResourceStrings.AliasUpdated);
    }

    private void CopyAppName(ChartsDataModel data)
    {
        var app = GetApp(data);
        if (app?.Name == null) return;

        _ = _clipboardService.SetTextAsync(app.Name);
        _mainViewModel?.Toast(ResourceStrings.OperationCompleted);
    }

    private void CopyAppFile(ChartsDataModel data)
    {
        var app = GetApp(data);
        if (app?.File == null) return;

        _ = _clipboardService.SetTextAsync(app.File);
        _mainViewModel?.Toast(ResourceStrings.OperationCompleted);
    }

    private async void ClearAppDataAsync(ChartsDataModel data)
    {
        var app = GetApp(data);
        if (app == null) return;

        var confirm = await _uiServicer.ShowConfirmDialogAsync(
            ResourceStrings.ClearConfirmation,
            string.Format(ResourceStrings.WantClearData, DateTime.Now.Year, DateTime.Now.Month));

        if (!confirm) return;

        _mainViewModel?.Toast(ResourceStrings.Processing);
        await _dataService.ClearAsync(app.ID, DateTime.Now);
        _mainViewModel?.RefreshCurrentPage();
        _mainViewModel?.Toast(ResourceStrings.Cleared);
    }

    private async void OnBlockApp(ChartsDataModel data)
    {
        var app = GetApp(data);
        if (app == null) return;

        var config = _appConfig.GetConfig();
        var ignoreList = config.Behavior.IgnoreProcessList;
        var isIgnored = ignoreList.Contains(app.Name);

        if (isIgnored)
            ignoreList.Remove(app.Name);
        else
            ignoreList.Add(app.Name);

        _mainViewModel?.Toast(string.Format(
            isIgnored ? ResourceStrings.IgnoringApplicationCancelled : ResourceStrings.ApplicationNowIgnored,
            app.Description), ToastType.Success);

        await _appConfig.SaveAsync();
        _mainViewModel?.RefreshCurrentPage();
    }

    private async void OnWhiteListApp(ChartsDataModel data)
    {
        var app = GetApp(data);
        if (app == null) return;

        var config = _appConfig.GetConfig();
        var whiteList = config.Behavior.ProcessWhiteList;
        var inWhitelist = whiteList.Contains(app.Name);

        if (inWhitelist)
            whiteList.Remove(app.Name);
        else
            whiteList.Add(app.Name);

        _mainViewModel?.Toast(
            inWhitelist ? $"{ResourceStrings.RemovedApplicationFromWhitelist} {app.Description}"
                        : $"{ResourceStrings.AddedToWhitelist} {app.Description}",
            ToastType.Success);

        await _appConfig.SaveAsync();
    }

    private async Task PopulateAppCategorySubmenuAsync(MenuItem setCategoryMenuItem, int appId, int? currentCategoryId = null)
    {
        try
        {
            setCategoryMenuItem.Items.Clear();

            var app = await _appData.GetAppAsync(appId);
            var categoryId = app?.CategoryID ?? currentCategoryId ?? 0;
            var categories = await _categorys.GetCategoriesAsync();

            foreach (var category in categories.Where(c => !c.IsSystem))
            {
                var menuItem = new MenuItem
                {
                    ToggleType = MenuItemToggleType.Radio,
                    Header = category.Name,
                    IsChecked = categoryId == category.ID
                };
                menuItem.Click += async (_, _) => await SetAppCategoryAsync(appId, category);
                setCategoryMenuItem.Items.Add(menuItem);
            }

            var currentCategory = categories.FirstOrDefault(c => c.ID == categoryId);
            if (currentCategory != null && !currentCategory.IsSystem)
            {
                setCategoryMenuItem.Items.Add(new Separator());
                var sysCategory = categories.FirstOrDefault(c => c.IsSystem);
                var uncategorizedItem = new MenuItem
                {
                    Header = sysCategory?.Name ?? ResourceStrings.Uncategorized
                };
                uncategorizedItem.Click += async (_, _) => await ClearAppCategoryAsync(appId);
                setCategoryMenuItem.Items.Add(uncategorizedItem);
            }

            setCategoryMenuItem.Items.Add(new Separator());
            var newCategoryItem = new MenuItem { Header = ResourceStrings.NewCategory };
            newCategoryItem.Click += async (_, _) => await CreateNewAppCategoryAsync(appId, categories);
            setCategoryMenuItem.Items.Add(newCategoryItem);

            setCategoryMenuItem.IsEnabled = setCategoryMenuItem.Items.Count > 0;
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to load app categories: {ex.Message}", ex);
        }
    }

    private async Task SetAppCategoryAsync(int appId, CategoryModel category)
    {
        await _appUpdateService.UpdateCategoryAsync(appId, category.ID);
    }

    private async Task ClearAppCategoryAsync(int appId)
    {
        await _appUpdateService.ClearCategoryAsync(appId);
    }

    private async Task CreateNewAppCategoryAsync(int appId, IReadOnlyList<CategoryModel> existingCategories)
    {
        var result = await _uiServicer.ShowCreateCategoryDialogAsync(
            ResourceStrings.NewCategory,
            null,
            existingCategories.Select(c => c.Name),
            existingCategories.Select(c => c.Color));

        if (result == null) return;

        var newCategory = await _categorys.CreateAsync(new CategoryModel
        {
            Name = result.Name,
            IconFile = result.IconFile,
            Color = result.Color,
            IsDirectoryMatch = false
        });

        if (newCategory == null)
        {
            _mainViewModel?.Error(ResourceStrings.CreationFailed);
            return;
        }

        await _appUpdateService.UpdateCategoryAsync(appId, newCategory.ID);
        _mainViewModel?.Success(ResourceStrings.CategoryCreated);
    }

    #endregion

    #region WebSite Menu

    private async Task<ContextMenu> BuildWebSiteMenuAsync(ChartsDataModel data)
    {
        var menu = new ContextMenu();

        var site = GetSite(data);

        var siteItem = new MenuItem { IsEnabled = false, Header = site?.Title ?? "" };
        var blockItem = new MenuItem { Header = ResourceStrings.IgnoreSite };
        blockItem.Click += (_, _) => OnBlockWebSite(data);
        var setCategoryItem = new MenuItem { Header = ResourceStrings.SetCategory };

        if (site != null)
            await PopulateWebSiteCategorySubmenuAsync(setCategoryItem, site.ID);

        menu.Opening += (_, _) =>
        {
            var s = GetSite(data);
            if (s != null)
                siteItem.Header = s.Title;
            UpdateWebSiteBlockState(data, blockItem);
        };

        menu.Items.Add(siteItem);
        menu.Items.Add(CreateMenuItem(ResourceStrings.OpenWebsite, () => OpenWebSite(data)));
        menu.Items.Add(new Separator());
        menu.Items.Add(setCategoryItem);
        menu.Items.Add(CreateMenuItem(ResourceStrings.EditAlias, () => EditWebSiteAliasAsync(data)));
        menu.Items.Add(new Separator());
        menu.Items.Add(blockItem);

        return menu;
    }

    private void UpdateWebSiteBlockState(ChartsDataModel data, MenuItem blockItem)
    {
        var site = GetSite(data);
        if (site == null) return;

        var config = _appConfig.GetConfig();
        var isIgnored = config.Behavior.IgnoreUrlList.Contains(site.Domain);
        blockItem.Header = isIgnored ? ResourceStrings.UnignoreSite : ResourceStrings.IgnoreSite;
        blockItem.IsEnabled = !IsRegexIgnored(site.Domain);
    }

    private ContextMenu BuildWebSiteDetailMenu(ChartsDataModel data)
    {
        var menu = new ContextMenu();

        var site = GetSite(data);

        var blockItem = new MenuItem { Header = ResourceStrings.IgnoreSite };
        blockItem.Click += (_, _) => OnBlockWebSite(data);
        var setCategoryItem = new MenuItem { Header = ResourceStrings.SetCategory };

        menu.Opening += (_, _) => UpdateWebSiteBlockState(data, blockItem);

        menu.Items.Add(CreateMenuItem(ResourceStrings.OpenWebsite, () => OpenWebSite(data)));
        menu.Items.Add(CreateMenuItem(ResourceStrings.Refresh, RefreshCurrentPage));
        menu.Items.Add(CreateMenuItem(ResourceStrings.CopyDomain, () => CopyDomain(data)));
        menu.Items.Add(new Separator());
        menu.Items.Add(setCategoryItem);
        menu.Items.Add(CreateMenuItem(ResourceStrings.EditAlias, () => EditWebSiteAliasAsync(data)));
        menu.Items.Add(new Separator());
        menu.Items.Add(blockItem);
        menu.Items.Add(CreateMenuItem(ResourceStrings.ClearStatistics, () => ClearWebSiteDataAsync(data)));

        return menu;
    }

    private async Task<ContextMenu> BuildWebSiteDetailMenuAsync(ChartsDataModel data)
    {
        var menu = new ContextMenu();

        var site = GetSite(data);

        var blockItem = new MenuItem { Header = ResourceStrings.IgnoreSite };
        blockItem.Click += (_, _) => OnBlockWebSite(data);
        var setCategoryItem = new MenuItem { Header = ResourceStrings.SetCategory };

        if (site != null)
            await PopulateWebSiteCategorySubmenuAsync(setCategoryItem, site.ID);

        menu.Opening += (_, _) => UpdateWebSiteBlockState(data, blockItem);

        menu.Items.Add(CreateMenuItem(ResourceStrings.OpenWebsite, () => OpenWebSite(data)));
        menu.Items.Add(CreateMenuItem(ResourceStrings.Refresh, RefreshCurrentPage));
        menu.Items.Add(CreateMenuItem(ResourceStrings.CopyDomain, () => CopyDomain(data)));
        menu.Items.Add(new Separator());
        menu.Items.Add(setCategoryItem);
        menu.Items.Add(CreateMenuItem(ResourceStrings.EditAlias, () => EditWebSiteAliasAsync(data)));
        menu.Items.Add(new Separator());
        menu.Items.Add(blockItem);
        menu.Items.Add(CreateMenuItem(ResourceStrings.ClearStatistics, () => ClearWebSiteDataAsync(data)));

        return menu;
    }

    private static WebSiteModel? GetSite(ChartsDataModel data) => data?.Data as WebSiteModel;

    private void OpenWebSite(ChartsDataModel data)
    {
        var site = GetSite(data);
        if (site == null || string.IsNullOrEmpty(site.Domain)) return;

        _mainViewModel?.Info(ResourceStrings.OperationCompleted);
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = $"http://{site.Domain}",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to open website URL: " + ex);
        }
    }

    private void CopyDomain(ChartsDataModel data)
    {
        var site = GetSite(data);
        if (site == null || string.IsNullOrEmpty(site.Domain)) return;

        _ = _clipboardService.SetTextAsync(site.Domain);
        _mainViewModel?.Toast(ResourceStrings.OperationCompleted);
    }

    private async void EditWebSiteAliasAsync(ChartsDataModel data)
    {
        var site = GetSite(data);
        if (site == null) return;

        var input = await _uiServicer.ShowInputModalAsync(
            ResourceStrings.EditAlias,
            ResourceStrings.EnterAlias,
            site.Alias,
            ValidateAliasLength);

        if (input == null) return;

        site = site with { Alias = input };
        var updated = await _webData.UpdateAsync(site);
        
        if (updated != null)
        {
            _appEventService.PublishWebSiteChanged(updated, AppChangeType.Alias);
            _mainViewModel?.Success(ResourceStrings.AliasUpdated);
        }
    }

    private async void OnBlockWebSite(ChartsDataModel data)
    {
        var site = GetSite(data);
        if (site == null) return;

        var config = _appConfig.GetConfig();
        var isIgnored = config.Behavior.IgnoreUrlList.Contains(site.Domain);

        if (isIgnored)
            config.Behavior.IgnoreUrlList.Remove(site.Domain);
        else
            config.Behavior.IgnoreUrlList.Add(site.Domain);

        _mainViewModel?.Toast(string.Format(
            isIgnored ? ResourceStrings.UnignoredDomain : ResourceStrings.IgnoredDomain,
            site.Domain), ToastType.Success);

        await _appConfig.SaveAsync();
        _mainViewModel?.RefreshCurrentPage();
    }

    private async void ClearWebSiteDataAsync(ChartsDataModel data)
    {
        var site = GetSite(data);
        if (site == null) return;

        var confirm = await _uiServicer.ShowConfirmDialogAsync(
            ResourceStrings.ClearConfirmation,
            ResourceStrings.ClearAllStatisticsSiteTip);

        if (!confirm) return;

        await _webData.ClearAsync(site.ID);
        _mainViewModel?.Toast(ResourceStrings.OperationCompleted);
    }

    private async Task PopulateWebSiteCategorySubmenuAsync(MenuItem setCategoryMenuItem, int siteId)
    {
        try
        {
            setCategoryMenuItem.Items.Clear();

            var site = await _webData.GetWebSiteAsync(siteId);
            var categories = await _webData.GetWebSiteCategoriesAsync();
            var siteCategoryId = site?.CategoryID ?? 0;

            foreach (var category in categories.Where(c => !c.IsSystem))
            {
                var menuItem = new MenuItem
                {
                    ToggleType = MenuItemToggleType.Radio,
                    Header = category.Name,
                    IsChecked = siteCategoryId == category.ID
                };
                menuItem.Click += async (_, _) => await SetWebSiteCategoryAsync(siteId, category.ID);
                setCategoryMenuItem.Items.Add(menuItem);
            }

            var currentCategory = categories.FirstOrDefault(c => c.ID == siteCategoryId);
            if (currentCategory != null && !currentCategory.IsSystem)
            {
                setCategoryMenuItem.Items.Add(new Separator());
                var sysCategory = categories.FirstOrDefault(c => c.IsSystem);
                var uncategorizedItem = new MenuItem
                {
                    Header = sysCategory?.Name ?? ResourceStrings.Uncategorized
                };
                uncategorizedItem.Click += async (_, _) => await ClearWebSiteCategoryAsync(siteId);
                setCategoryMenuItem.Items.Add(uncategorizedItem);
            }

            setCategoryMenuItem.Items.Add(new Separator());
            var newCategoryItem = new MenuItem { Header = ResourceStrings.NewCategory };
            newCategoryItem.Click += async (_, _) => await CreateNewWebSiteCategoryAsync(siteId, categories);
            setCategoryMenuItem.Items.Add(newCategoryItem);
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to load website categories: {ex.Message}", ex);
        }
    }

    private async Task SetWebSiteCategoryAsync(int siteId, int categoryId)
    {
        await _webData.UpdateWebSitesCategoryAsync(new[] { siteId }, categoryId);
        
        var site = await _webData.GetWebSiteAsync(siteId);
        if (site != null)
        {
            _appEventService.PublishWebSiteChanged(site, AppChangeType.WebSiteCategory);
        }
    }

    private async Task ClearWebSiteCategoryAsync(int siteId)
    {
        var categories = await _webData.GetWebSiteCategoriesAsync();
        var systemCategory = categories.FirstOrDefault(c => c.IsSystem);
        var categoryId = systemCategory?.ID ?? 0;

        await _webData.UpdateWebSitesCategoryAsync(new[] { siteId }, categoryId);
        
        var site = await _webData.GetWebSiteAsync(siteId);
        if (site != null)
        {
            _appEventService.PublishWebSiteChanged(site, AppChangeType.WebSiteCategory);
        }
    }

    private async Task CreateNewWebSiteCategoryAsync(int siteId, IReadOnlyList<WebSiteCategoryModel> existingCategories)
    {
        var result = await _uiServicer.ShowCreateCategoryDialogAsync(
            ResourceStrings.NewCategory,
            null,
            existingCategories.Select(c => c.Name),
            existingCategories.Select(c => c.Color));

        if (result == null) return;

        var newCategory = await _webData.CreateWebSiteCategoryAsync(new WebSiteCategoryModel
        {
            Name = result.Name,
            IconFile = result.IconFile,
            Color = result.Color
        });

        if (newCategory == null)
        {
            _mainViewModel?.Error(ResourceStrings.CreationFailed);
            return;
        }

        await SetWebSiteCategoryAsync(siteId, newCategory.ID);
        _mainViewModel?.Success(ResourceStrings.CategoryCreated);
    }

    #endregion

    #region Helpers

    private bool ValidateAliasLength(string? alias)
    {
        if (alias?.Length > 15)
        {
            _mainViewModel?.Error(string.Format(ResourceStrings.AliasMaxLengthTip, 15));
            return false;
        }
        return true;
    }

    private void RefreshCurrentPage() => _mainViewModel?.RefreshCurrentPage();

    private static MenuItem CreateMenuItem(string header, Action clickAction)
    {
        var menuItem = new MenuItem { Header = header };
        menuItem.Click += (_, _) => clickAction();
        return menuItem;
    }

    private void OnGlobalPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(null).Properties.IsLeftButtonPressed && sender is Visual visual)
            CloseAllContextMenus(visual);
    }

    private static void CloseAllContextMenus(Visual visual)
    {
        if (visual is Control { ContextMenu.IsOpen: true } control)
        {
            control.ContextMenu.Close();
            return;
        }

        foreach (var child in visual.GetVisualChildren())
            CloseAllContextMenus(child);
    }

    private bool IsRegexIgnored(string url) =>
        _appConfig.GetConfig().Behavior.IgnoreUrlList
            .Any(item => IsRegexPattern(item) && IsRegexMatch(url, item));

    private static bool IsRegexMatch(string input, string pattern)
    {
        try
        {
            return Regex.IsMatch(input, pattern,
                RegexOptions.IgnoreCase | RegexOptions.Compiled,
                TimeSpan.FromSeconds(1));
        }
        catch (RegexMatchTimeoutException) { return false; }
        catch (ArgumentException) { return false; }
    }

    private static bool IsRegexPattern(string pattern) =>
        !string.IsNullOrEmpty(pattern) && Regex.IsMatch(pattern, @"[\.\*\?\{\\\[\^\|]");

    #endregion
}

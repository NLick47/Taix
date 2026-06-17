using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Taix.Client.Controls.Base;
using Taix.Client.Controls.Charts.Model;
using Taix.Client.Controls.Window;
using Taix.Client.Logging;
using Taix.Client.Platform;
using Taix.Client.Shared.Helpers;
using Taix.Client.Shared.Models;
using Taix.Client.Shared.Servicers.Interfaces;
using Taix.Client.ViewModels;
using Taix.Client.Views;

namespace Taix.Client.Servicers;

public class AppContextMenuServicer : IAppContextMenuServicer, IDisposable
{
    private readonly IUIServicer _uiServicer;
    private readonly IAppConfig _appConfig;
    private readonly IAppData _appData;
    private readonly ICategorys _categorys;
    private MainViewModel? _mainViewModel;

    private ContextMenu _contextMenu;
    private MenuItem _runMenuItem;
    private MenuItem _openDirMenuItem;
    private MenuItem _setCategoryMenuItem;
    private MenuItem _editAliasMenuItem;
    private MenuItem _blockMenuItem;
    private MenuItem _whiteListMenuItem;
    private IDisposable? _languageSubscription;

    public AppContextMenuServicer(
        ICategorys categorys,
        IAppData appData,
        IAppConfig appConfig,
        IUIServicer uiServicer)
    {
        _categorys = categorys;
        _appData = appData;
        _appConfig = appConfig;
        _uiServicer = uiServicer;
    }

    private static MainWindow? GetMainWindow()
    {
        var desk = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        return desk?.MainWindow as MainWindow;
    }

    public void Init()
    {
        var mainWindow = GetMainWindow();
        if (mainWindow != null)
        {
            mainWindow.PointerPressed += OnGlobalPointerPressed;
        }
        _languageSubscription = _appConfig.WhenLanguageChanged(OnLanguageChanged);
        _mainViewModel = ServiceLocator.GetService<MainViewModel>();
    }

    public void Dispose()
    {
        var mainWindow = GetMainWindow();
        if (mainWindow != null)
        {
            mainWindow.PointerPressed -= OnGlobalPointerPressed;
        }
        _languageSubscription?.Dispose();
    }

    public ContextMenu GetContextMenu()
    {
        if (_contextMenu == null)
        {
            InitializeContextMenu();
        }
        return _contextMenu;
    }

    private void OnGlobalPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(null).Properties.IsLeftButtonPressed && sender is Visual visual)
        {
            CloseAllContextMenus(visual);
        }
    }

    private void OnLanguageChanged() => UpdateMenuTexts();

    private void InitializeContextMenu()
    {
        _contextMenu = new ContextMenu();
        CreateMenuItems();
        SetupMenuStructure();
        ApplyPlatformVisibility();
        AttachEventHandlers();
        UpdateMenuTexts();
    }

    private void ApplyPlatformVisibility()
    {
        _openDirMenuItem.IsVisible = !PlatformHelper.IsMacOS;
    }

    private void CreateMenuItems()
    {
        _runMenuItem = CreateMenuItem(RunMenuItem_Click);
        _openDirMenuItem = CreateMenuItem(OpenDirMenuItem_Click);
        _setCategoryMenuItem = CreateMenuItem();
        _editAliasMenuItem = CreateMenuItem(EditAliasMenuItem_Click);
        _blockMenuItem = CreateMenuItem(BlockMenuItem_Click);
        _whiteListMenuItem = CreateMenuItem(WhiteListMenuItem_Click);
    }

    private MenuItem CreateMenuItem(EventHandler<PointerPressedEventArgs>? c = null)
    {
        var menuItem = new MenuItem();
        if (c != null)
            menuItem.PointerPressed += c;
        return menuItem;
    }

    private void SetupMenuStructure()
    {
        _contextMenu.Items.Add(_runMenuItem);
        _contextMenu.Items.Add(new Separator());
        _contextMenu.Items.Add(_setCategoryMenuItem);
        _contextMenu.Items.Add(_editAliasMenuItem);
        _contextMenu.Items.Add(new Separator());
        _contextMenu.Items.Add(_openDirMenuItem);
        _contextMenu.Items.Add(_blockMenuItem);
        _contextMenu.Items.Add(_whiteListMenuItem);
        _contextMenu.Opening += OnContextMenuOpening;
    }

    private void AttachEventHandlers()
    {
        // 事件处理器已在CreateMenuItem中附加
    }

    private void UpdateMenuTexts()
    {
        _runMenuItem.Header = ResourceStrings.StartApplication;
        _openDirMenuItem.Header = ResourceStrings.OpenApplicationDirectory;
        _setCategoryMenuItem.Header = ResourceStrings.SetCategory;
        _editAliasMenuItem.Header = ResourceStrings.EditAlias;

        _blockMenuItem.Header = ResourceStrings.IgnoreThisApplication;
        _whiteListMenuItem.Header = ResourceStrings.AddWhitelist;
    }

    private async void OnContextMenuOpening(object? sender, CancelEventArgs e)
    {
        try
        {
            await OnContextMenuOpeningAsync();
        }
        catch (Exception ex)
        {
            Logger.Error($"上下文菜单打开失败: {ex.Message}", ex);
        }
    }

    private async Task OnContextMenuOpeningAsync()
    {
        if (_contextMenu.Tag == null) return;

        var app = GetAppFromContextMenu();
        if (app == null) return;

        UpdateBlockMenuItemText(app);
        UpdateWhiteListMenuItemText(app);
        await UpdateCategoryMenuItemsAsync();
    }

    private AppModel? GetAppFromContextMenu()
    {
        var data = _contextMenu.Tag as ChartsDataModel;
        if (data?.Data == null) return null;

        return data.Data switch
        {
            DailyLogModel dailyLog => dailyLog.AppModel,
            HoursLogModel hoursLog => hoursLog.AppModel,
            _ => null
        };
    }

    private void UpdateBlockMenuItemText(AppModel app)
    {
        var config = _appConfig.GetConfig();
        _blockMenuItem.Header = config.Behavior.IgnoreProcessList.Contains(app.Name)
            ? ResourceStrings.Unignore
            : ResourceStrings.IgnoreThisApplication;
    }

    private void UpdateWhiteListMenuItemText(AppModel app)
    {
        var config = _appConfig.GetConfig();
        _whiteListMenuItem.Header = config.Behavior.ProcessWhiteList.Contains(app.Name)
            ? ResourceStrings.RemoveWhitelist
            : ResourceStrings.AddWhitelist;
    }

    private async Task UpdateCategoryMenuItemsAsync()
    {
        _setCategoryMenuItem.Items.Clear();

        var app = GetAppFromContextMenu();
        if (app == null) return;

        var categories = await _categorys.GetCategoriesAsync();

        var appCategoryId = app?.CategoryID ?? 0;

        foreach (var category in categories)
        {
            if (category.IsSystem) continue;
            var categoryMenuItem = CreateCategoryMenuItem(category, appCategoryId, app.ID);
            _setCategoryMenuItem.Items.Add(categoryMenuItem);
        }

        var currentCategory = categories.FirstOrDefault(c => c.ID == appCategoryId);
        if (currentCategory != null && !currentCategory.IsSystem)
        {
            _setCategoryMenuItem.Items.Add(new Separator());
            var sysCategory = categories.FirstOrDefault(c => c.IsSystem);
            var uncategorizedMenuItem = new MenuItem
            {
                Header = sysCategory?.Name ?? ResourceStrings.Uncategorized
            };
            uncategorizedMenuItem.Click += async (s, e) => await ClearAppCategoryAsync(app.ID);
            _setCategoryMenuItem.Items.Add(uncategorizedMenuItem);
        }

        // 添加新建分类选项
        _setCategoryMenuItem.Items.Add(new Separator());
        var newCategoryMenuItem = new MenuItem
        {
            Header = ResourceStrings.NewCategory
        };
        newCategoryMenuItem.Click += async (s, e) => await CreateNewCategoryAndAssignAppAsync(categories);
        _setCategoryMenuItem.Items.Add(newCategoryMenuItem);

        _setCategoryMenuItem.IsEnabled = true;
    }

    private MenuItem CreateCategoryMenuItem(CategoryModel category, int appCategoryId, int appId)
    {
        var menuItem = new MenuItem
        {
            ToggleType = MenuItemToggleType.Radio,
            Header = category.Name,
            IsChecked = appCategoryId == category.ID,
            IsSelected = true
        };

        menuItem.Click += async (s, e) => await SetAppCategoryAsync(appId, category);
        return menuItem;
    }



    private async void EditAliasMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            await EditAliasAsync();
        }
        catch (Exception ex)
        {
            Logger.Error($"编辑别名失败: {ex.Message}", ex);
        }
    }

    private async Task EditAliasAsync()
    {
        var app = GetAppFromContextMenu();
        if (app == null) return;

        var input = await _uiServicer.ShowInputModalAsync(
            ResourceStrings.UpdateAlias,
            ResourceStrings.EnterAlias,
            app.Alias,
            ValidateAlias);

        if (input != null)
        {
            await UpdateAppAlias(app, input);
        }
    }

    private bool ValidateAlias(string alias)
    {
        if (alias?.Length > 15)
        {
            _mainViewModel?.Error(string.Format(ResourceStrings.AliasMaxLengthTip, 15));
            return false;
        }
        return true;
    }

    private async Task UpdateAppAlias(AppModel app, string newAlias)
    {
        var appToUpdate = await _appData.GetAppAsync(app.ID);
        if (appToUpdate == null) return;
        appToUpdate = appToUpdate with { Alias = newAlias };
        await _appData.UpdateAppAsync(appToUpdate);

        // 更新UI显示
        var data = _contextMenu.Tag as ChartsDataModel;
        if (data != null)
        {
            data.Name = string.IsNullOrEmpty(newAlias) ? appToUpdate.Description : newAlias;
        }

        _mainViewModel?.Success(ResourceStrings.AliasUpdated);
    }

    private async void BlockMenuItem_Click(object? sender, PointerPressedEventArgs e)
    {
        var app = GetAppFromContextMenu();
        if (app == null) return;

        await ToggleAppIgnoreStatusAsync(app);
    }

    private async Task ToggleAppIgnoreStatusAsync(AppModel app)
    {
        var config = _appConfig.GetConfig();
        var data = _contextMenu.Tag as ChartsDataModel;
        var ignoreList = config.Behavior.IgnoreProcessList;

        if (ignoreList.Contains(app.Name))
        {
            ignoreList.Remove(app.Name);
            RemoveIgnoreBadge(data);
            _mainViewModel?.Toast(string.Format(ResourceStrings.IgnoringApplicationCancelled, app.Description), ToastType.Success);
        }
        else
        {
            ignoreList.Add(app.Name);
            AddIgnoreBadge(data);
            _mainViewModel?.Toast(string.Format(ResourceStrings.ApplicationNowIgnored, app.Description), ToastType.Success);
        }

        await _appConfig.SaveAsync();
    }

    private void AddIgnoreBadge(ChartsDataModel data)
    {
        var newBadgeList = data.BadgeList?.Where(m => m.Type != ChartBadgeType.Ignore).ToList() ?? new List<ChartBadgeModel>();
        newBadgeList.Add(ChartBadgeModel.IgnoreBadge);
        data.BadgeList = newBadgeList;
    }

    private void RemoveIgnoreBadge(ChartsDataModel data)
    {
        var newBadgeList = data.BadgeList?.Where(m => m.Type != ChartBadgeType.Ignore).ToList();
        data.BadgeList = newBadgeList ?? new List<ChartBadgeModel>();
    }

    private async void WhiteListMenuItem_Click(object? sender, PointerPressedEventArgs e)
    {
        var app = GetAppFromContextMenu();
        if (app == null) return;

        await ToggleAppWhiteListStatusAsync(app);
    }

    private async Task ToggleAppWhiteListStatusAsync(AppModel app)
    {
        var config = _appConfig.GetConfig();
        var whiteList = config.Behavior.ProcessWhiteList;

        if (whiteList.Contains(app.Name))
        {
            whiteList.Remove(app.Name);
            _mainViewModel?.Toast($"{ResourceStrings.RemovedApplicationFromWhitelist} {app.Description}", ToastType.Success);
        }
        else
        {
            whiteList.Add(app.Name);
            _mainViewModel?.Toast($"{ResourceStrings.AddedToWhitelist} {app.Description}", ToastType.Success);
        }

        await _appConfig.SaveAsync();
    }

    private async Task SetAppCategoryAsync(int appId, CategoryModel category)
    {
        var data = _contextMenu.Tag as ChartsDataModel;
        UpdateCategoryBadge(data, category);

        var app = await _appData.GetAppAsync(appId);
        if (app == null) return;
        var updatedApp = app with { CategoryID = category.ID, Category = category };
        await _appData.UpdateAppAsync(updatedApp);

        if (data?.Data is DailyLogModel dailyLog)
            dailyLog.AppModel = updatedApp;
        else if (data?.Data is HoursLogModel hoursLog)
            hoursLog.AppModel = updatedApp;
    }

    private async Task CreateNewCategoryAndAssignAppAsync(IReadOnlyList<CategoryModel> existingCategories)
    {
        var app = GetAppFromContextMenu();
        if (app == null) return;

        var existingNames = existingCategories.Select(c => c.Name);
        var existingColors = existingCategories.Select(c => c.Color);

        var result = await _uiServicer.ShowCreateCategoryDialogAsync(
            ResourceStrings.NewCategory,
            null,
            existingNames,
            existingColors);

        if (result == null) return;

        // 创建新分类
        var newCategory = await _categorys.CreateAsync(new CategoryModel
        {
            Name = result.Name,
            IconFile = result.IconFile,
            Color = result.Color,
            IsDirectoryMatch = false,
            Directories = null
        });

        if (newCategory == null)
        {
            _mainViewModel?.Error(ResourceStrings.CreationFailed);
            return;
        }

        // 自动将应用分配到新分类
        await SetAppCategoryAsync(app.ID, newCategory);

        _mainViewModel?.Success(ResourceStrings.CategoryCreated);
    }

    private void UpdateCategoryBadge(ChartsDataModel? data, CategoryModel category)
    {
        if(data == null) return;
        var newBadgeList = data.BadgeList?.Where(m => m.Type != ChartBadgeType.Category).ToList() ?? new List<ChartBadgeModel>();
        newBadgeList.Add(new ChartBadgeModel
        {
            Name = category.Name,
            Color = category.Color,
            Type = ChartBadgeType.Category
        });
        data.BadgeList = newBadgeList;
    }

    private async Task ClearAppCategoryAsync(int appId)
    {
        var data = _contextMenu.Tag as ChartsDataModel;
        if(data == null) return;
        data.BadgeList = new List<ChartBadgeModel>();
        var app = await _appData.GetAppAsync(appId);
        if (app == null) return;
        var updatedApp = app with { CategoryID = 0, Category = null };
        await _appData.UpdateAppAsync(updatedApp);

        if (data.Data is DailyLogModel dailyLog)
            dailyLog.AppModel = updatedApp;
        else if (data.Data is HoursLogModel hoursLog)
            hoursLog.AppModel = updatedApp;
    }

    private void OpenDirMenuItem_Click(object? sender, PointerPressedEventArgs e)
    {
        var app = GetAppFromContextMenu();
        if (app == null) return;

        OpenAppDirectory(app);
    }

    private void OpenAppDirectory(AppModel app)
    {
        if (File.Exists(app.File))
        {
            PlatformHelper.OpenFileInExplorer(app.File);
        }
        else
        {
            _mainViewModel?.Toast(ResourceStrings.ApplicationFileExist, ToastType.Error, IconTypes.IncidentTriangle);
        }
    }

    private void RunMenuItem_Click(object? sender, PointerPressedEventArgs e)
    {
        var app = GetAppFromContextMenu();
        if (app == null) return;
        RunApplication(app);
    }

    private void RunApplication(AppModel app)
    {
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

    private void CloseAllContextMenus(Visual visual)
    {
        if (visual is Control control && control.ContextMenu?.IsOpen == true)
        {
            control.ContextMenu.Close();
            return;
        }

        foreach (var child in visual.GetVisualChildren())
        {
            CloseAllContextMenus(child);
        }
    }
}

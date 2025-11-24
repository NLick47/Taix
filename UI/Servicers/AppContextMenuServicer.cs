using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Core.Models;
using Core.Servicers.Interfaces;
using SharedLibrary;
using UI.Controls.Base;
using UI.Controls.Charts.Model;
using UI.Controls.Window;
using UI.ViewModels;
using UI.Views;

namespace UI.Servicers;

public class AppContextMenuServicer : IAppContextMenuServicer
{
    private readonly MainWindow _mainWindow;
    private readonly IUIServicer _uiServicer;
    private readonly IAppConfig _appConfig;
    private readonly IAppData _appData;
    private readonly ICategorys _categorys;
    private readonly MainViewModel _mainViewModel;
    private readonly IThemeServicer _themeServicer;
    
    private ContextMenu _contextMenu;
    private MenuItem _runMenuItem;
    private MenuItem _openDirMenuItem;
    private MenuItem _setCategoryMenuItem;
    private MenuItem _editAliasMenuItem;
    private MenuItem _setLinkMenuItem;
    private MenuItem _blockMenuItem;
    private MenuItem _whiteListMenuItem;

    public AppContextMenuServicer(
        MainViewModel mainViewModel,
        ICategorys categorys,
        IAppData appData,
        IAppConfig appConfig,
        IThemeServicer themeServicer,
        MainWindow mainWindow,
        IUIServicer uiServicer)
    {
        _mainViewModel = mainViewModel;
        _categorys = categorys;
        _appData = appData;
        _appConfig = appConfig;
        _themeServicer = themeServicer;
        _uiServicer = uiServicer;
        _mainWindow = mainWindow;
    }

    public void Init()
    {
        InitializeContextMenu();
        _mainWindow.PointerPressed += OnGlobalPointerPressed;
        SystemLanguage.CurrentLanguageChanged += OnLanguageChanged;
    }

    public ContextMenu GetContextMenu() => _contextMenu;

    private void OnGlobalPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(null).Properties.IsLeftButtonPressed)
        {
            CloseAllContextMenus(_mainWindow);
        }
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => UpdateMenuTexts();

    private void InitializeContextMenu()
    {
        _contextMenu = new ContextMenu();
        CreateMenuItems();
        SetupMenuStructure();
        AttachEventHandlers();
        UpdateMenuTexts();
    }

    private void CreateMenuItems()
    {
        _runMenuItem = CreateMenuItem(RunMenuItem_Click);
        _openDirMenuItem = CreateMenuItem(OpenDirMenuItem_Click);
        _setCategoryMenuItem = CreateMenuItem();
        _editAliasMenuItem = CreateMenuItem(EditAliasMenuItem_Click);
        _setLinkMenuItem = CreateMenuItem();
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
        _contextMenu.Items.Add(_setLinkMenuItem);
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
        _setLinkMenuItem.Header = ResourceStrings.AddAssociation;
        _blockMenuItem.Header = ResourceStrings.IgnoreThisApplication;
        _whiteListMenuItem.Header = ResourceStrings.AddWhitelist;
    }

    private void OnContextMenuOpening(object? sender, CancelEventArgs e)
    {
        if (_contextMenu.Tag == null) return;
        
        var app = GetAppFromContextMenu();
        if (app == null) return;

        UpdateBlockMenuItemText(app);
        UpdateWhiteListMenuItemText(app);
        UpdateCategoryMenuItems();
        UpdateLinkMenuItems();
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

    private void UpdateCategoryMenuItems()
    {
        _setCategoryMenuItem.Items.Clear();

        var app = GetAppFromContextMenu();
        if (app == null) return;

        var categories = _categorys.GetCategories();
        var appCategoryId = _appData.GetApp(app.ID).CategoryID;

        foreach (var category in categories)
        {
            var categoryMenuItem = CreateCategoryMenuItem(category, appCategoryId, app.ID);
            _setCategoryMenuItem.Items.Add(categoryMenuItem);
        }

        if (appCategoryId != 0)
        {
            _setCategoryMenuItem.Items.Add(new Separator());
            var uncategorizedMenuItem = CreateUncategorizedMenuItem(app.ID);
            _setCategoryMenuItem.Items.Add(uncategorizedMenuItem);
        }

        _setCategoryMenuItem.IsEnabled = _setCategoryMenuItem.Items.Count > 0;
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

        menuItem.Click += (s, e) => SetAppCategory(appId, category);
        return menuItem;
    }

    private MenuItem CreateUncategorizedMenuItem(int appId)
    {
        var menuItem = new MenuItem
        {
            Header = ResourceStrings.Uncategorized
        };

        menuItem.Click += (s, e) => ClearAppCategory(appId);
        return menuItem;
    }

    private void UpdateLinkMenuItems()
    {
        _setLinkMenuItem.Items.Clear();

        var app = GetAppFromContextMenu();
        if (app == null) return;

        var config = _appConfig.GetConfig();
        var links = config.Links;

        foreach (var link in links)
        {
            var linkMenuItem = new MenuItem
            {
                Header = link.Name
            };
            linkMenuItem.Click += (_, _) => SetAppLink(app, link.Name);
            _setLinkMenuItem.Items.Add(linkMenuItem);
        }

        _setLinkMenuItem.IsEnabled = links.Count > 0;
    }

    private async void EditAliasMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        var app = GetAppFromContextMenu();
        if (app == null) return;

        try
        {
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
        catch
        {
            // 输入取消，无需处理异常
        }
    }

    private bool ValidateAlias(string alias)
    {
        if (alias?.Length > 15)
        {
            _mainViewModel.Error(string.Format(ResourceStrings.AliasMaxLengthTip, 15));
            return false;
        }
        return true;
    }

    private async System.Threading.Tasks.Task UpdateAppAlias(AppModel app, string newAlias)
    {
        var appToUpdate = _appData.GetApp(app.ID);
        appToUpdate.Alias = newAlias;
        _appData.UpdateApp(appToUpdate);

        // 更新UI显示
        var data = _contextMenu.Tag as ChartsDataModel;
        if (data != null)
        {
            data.Name = string.IsNullOrEmpty(newAlias) ? appToUpdate.Description : newAlias;
        }

        _mainViewModel.Success(ResourceStrings.AliasUpdated);
    }

    private void BlockMenuItem_Click(object? sender, PointerPressedEventArgs e)
    {
        var app = GetAppFromContextMenu();
        if (app == null) return;

        ToggleAppIgnoreStatus(app);
    }

    private void ToggleAppIgnoreStatus(AppModel app)
    {
        var config = _appConfig.GetConfig();
        var data = _contextMenu.Tag as ChartsDataModel;
        var ignoreList = config.Behavior.IgnoreProcessList;

        if (ignoreList.Contains(app.Name))
        {
            ignoreList.Remove(app.Name);
            RemoveIgnoreBadge(data);
            _mainViewModel.Toast(string.Format(ResourceStrings.IgnoringApplicationCancelled, app.Description), ToastType.Success);
        }
        else
        {
            ignoreList.Add(app.Name);
            AddIgnoreBadge(data);
            _mainViewModel.Toast(string.Format(ResourceStrings.ApplicationNowIgnored, app.Description), ToastType.Success);
        }
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

    private void WhiteListMenuItem_Click(object? sender, PointerPressedEventArgs e)
    {
        var app = GetAppFromContextMenu();
        if (app == null) return;

        ToggleAppWhiteListStatus(app);
    }

    private void ToggleAppWhiteListStatus(AppModel app)
    {
        var config = _appConfig.GetConfig();
        var whiteList = config.Behavior.ProcessWhiteList;

        if (whiteList.Contains(app.Name))
        {
            whiteList.Remove(app.Name);
            _mainViewModel.Toast($"{ResourceStrings.RemovedApplicationFromWhitelist} {app.Description}", ToastType.Success);
        }
        else
        {
            whiteList.Add(app.Name);
            _mainViewModel.Toast($"{ResourceStrings.AddedToWhitelist} {app.Description}", ToastType.Success);
        }
    }

    private void SetAppCategory(int appId, CategoryModel category)
    {
        var data = _contextMenu.Tag as ChartsDataModel;
        UpdateCategoryBadge(data, category);

        var app = _appData.GetApp(appId);
        app.CategoryID = category.ID;
        app.Category = category;
        _appData.UpdateApp(app);
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

    private void ClearAppCategory(int appId)
    {
        var data = _contextMenu.Tag as ChartsDataModel;
        if(data == null) return;
        data.BadgeList = new List<ChartBadgeModel>();
        var app = _appData.GetApp(appId);
        app.CategoryID = 0;
        app.Category = null;
        _appData.UpdateApp(app);
    }

    private void SetAppLink(AppModel app, string linkName)
    {
        var config = _appConfig.GetConfig();
        var links = config.Links;

        // 移除现有的关联
        var existingLink = links.FirstOrDefault(m => m.ProcessList.Contains(app.Name));
        existingLink?.ProcessList.Remove(app.Name);

        // 添加新的关联
        var targetLink = links.FirstOrDefault(m => m.Name == linkName);
        if (targetLink != null)
        {
            targetLink.ProcessList.Add(app.Name);
            _appConfig.Save();
            _mainViewModel.Toast(ResourceStrings.AssociationSuccessful, ToastType.Success);
        }
        else
        {
            _mainViewModel.Toast(ResourceStrings.AssociationConfigurationNotExist, ToastType.Error, IconTypes.IncidentTriangle);
        }
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
            Process.Start("explorer.exe", "/select, " + app.File);
        }
        else
        {
            _mainViewModel.Toast(ResourceStrings.ApplicationFileExist, ToastType.Error, IconTypes.IncidentTriangle);
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
            Process.Start(app.File);
            _mainViewModel.Toast(ResourceStrings.OperationCompleted);
        }
        else
        {
            _mainViewModel.Toast(ResourceStrings.ApplicationFileExist, ToastType.Error, IconTypes.IncidentTriangle);
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
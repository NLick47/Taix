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
    private readonly IUIServicer _uIServicer;
    private readonly IAppConfig appConfig;
    private readonly IAppData appData;
    private readonly MenuItem block = new();
    private readonly ICategorys categorys;
    private readonly MainViewModel main;
    private readonly IThemeServicer theme;
    private readonly MenuItem whiteList = new();
    private MenuItem editAlias;
    private ContextMenu menu;
    private MenuItem openDir;
    private MenuItem run;
    private MenuItem setCategory;
    private MenuItem setLink;


    public AppContextMenuServicer(
        MainViewModel main,
        ICategorys categorys,
        IAppData appData,
        IAppConfig appConfig,
        IThemeServicer theme,
        MainWindow mainWindow,
        IUIServicer uIServicer_)
    {
        this.main = main;
        this.categorys = categorys;
        this.appData = appData;
        this.appConfig = appConfig;
        this.theme = theme;
        _uIServicer = uIServicer_;
        _mainWindow = mainWindow;
    }


    public void Init()
    {
        InitializeMenuItems();
        _mainWindow.PointerPressed += OnGlobalPointerPressed;
        SystemLanguage.CurrentLanguageChanged += (s, e) => UpdateMenuTexts();
    }

    public ContextMenu GetContextMenu()
    {
        return menu;
    }

    private void OnGlobalPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(null).Properties.IsLeftButtonPressed) CloseAllContextMenus(_mainWindow);
    }

    private void CloseAllContextMenus(Visual visual)
    {
        if (visual is Control ctl
            && ctl.ContextMenu != null
            && ctl.ContextMenu.IsOpen)
        {
            ctl.ContextMenu.Close();
            return;
        }

        foreach (var child in visual.GetVisualChildren()) CloseAllContextMenus(child);
    }

    private void InitializeMenuItems()
    {
        if (menu != null)
        {
            menu.Opening -= SetCategory_ContextMenuOpening;
            menu.Items.Clear();
        }

        menu = new ContextMenu();
        run = new MenuItem();
        run.PointerPressed += Run_Click;

        openDir = new MenuItem();
        openDir.PointerPressed += OpenDir_Click;
        setCategory = new MenuItem();
        editAlias = new MenuItem();
        editAlias.Click += EditAlias_ClickAsync;
        setLink = new MenuItem();
        block.PointerPressed += Block_Click;

        whiteList.PointerPressed += _whiteList_Click;

        menu.Items.Add(run);
        menu.Items.Add(new Separator());
        menu.Items.Add(setCategory);
        menu.Items.Add(setLink);
        menu.Items.Add(editAlias);
        menu.Items.Add(new Separator());

        menu.Items.Add(openDir);
        menu.Items.Add(block);
        menu.Items.Add(whiteList);

        menu.Opening += SetCategory_ContextMenuOpening;
        UpdateMenuTexts();
    }

    private void UpdateMenuTexts()
    {
        run.Header = ResourceStrings.StartApplication;
        openDir.Header = ResourceStrings.OpenApplicationDirectory;
        setCategory.Header = ResourceStrings.SetCategory;
        editAlias.Header = ResourceStrings.EditAlias;
        setLink.Header = ResourceStrings.AddAssociation;
        block.Header = ResourceStrings.IgnoreThisApplication;
        whiteList.Header = ResourceStrings.AddWhitelist;
    }

    private async void EditAlias_ClickAsync(object sender, RoutedEventArgs e)
    {
        var data = menu.Tag as ChartsDataModel;
        var log = data.Data as DailyLogModel;
        var app = log != null ? log.AppModel : null;

        if (log == null) app = (data.Data as HoursLogModel).AppModel;

        try
        {
            var input = await _uIServicer.ShowInputModalAsync(ResourceStrings.UpdateAlias, ResourceStrings.EnterAlias,
                app.Alias, val =>
                {
                    if (val?.Length > 15)
                    {
                        main.Error(string.Format(ResourceStrings.AliasMaxLengthTip, 15));
                        return false;
                    }

                    return true;
                });

            //  开始更新别名
            var editApp = appData.GetApp(app.ID);
            editApp.Alias = input;
            appData.UpdateApp(editApp);
            data.Name = string.IsNullOrEmpty(input) ? editApp.Description : input;

            main.Success(ResourceStrings.AliasUpdated);
            Debug.WriteLine("输入内容：" + input);
        }
        catch
        {
            //  输入取消，无需处理异常
        }
    }


    private void Block_Click(object sender, PointerPressedEventArgs e)
    {
        var data = menu.Tag as ChartsDataModel;
        var log = data.Data as DailyLogModel;
        var app = log != null ? log.AppModel : null;

        if (log == null) app = (data.Data as HoursLogModel).AppModel;
        var newBadgeList = new List<ChartBadgeModel>();
        if (data.BadgeList != null)
        {
            var categoryBadge = data.BadgeList.Where(m => m.Type != ChartBadgeType.Ignore).ToList();
            newBadgeList.AddRange(categoryBadge);
        }

        var config = appConfig.GetConfig();
        if (config.Behavior.IgnoreProcessList.Contains(app.Name))
        {
            config.Behavior.IgnoreProcessList.Remove(app.Name);
            main.Toast(string.Format(ResourceStrings.IgnoringApplicationCancelled, app.Description), ToastType.Success);
        }
        else
        {
            config.Behavior.IgnoreProcessList.Add(app.Name);
            main.Toast(string.Format(ResourceStrings.ApplicationNowIgnored, app.Description), ToastType.Success);
            newBadgeList.Add(ChartBadgeModel.IgnoreBadge);
        }

        data.BadgeList = newBadgeList;
    }

    private void SetCategory_ContextMenuOpening(object sender, CancelEventArgs e)
    {
        if (menu.Tag == null) return;
        var data = menu.Tag as ChartsDataModel;
        var log = data.Data as DailyLogModel;
        var app = log != null ? log.AppModel : null;

        if (log == null) app = (data.Data as HoursLogModel).AppModel;


        var config = appConfig.GetConfig();
        if (config.Behavior.IgnoreProcessList.Contains(app.Name))
            block.Header = ResourceStrings.Unignore;
        else
            block.Header = ResourceStrings.IgnoreThisApplication;

        if (config.Behavior.ProcessWhiteList.Contains(app.Name))
            whiteList.Header = ResourceStrings.RemoveWhitelist;
        else
            whiteList.Header = ResourceStrings.AddWhitelist;

        UpdateCategory();

        setLink.IsEnabled = config.Links.Count > 0;
        UpdateLinks();
    }


    private void UpdateCategory()
    {
        setCategory.Items.Clear();

        var data = menu.Tag as ChartsDataModel;
        var log = data.Data as DailyLogModel;
        var app = log != null ? log.AppModel : null;

        if (log == null) app = (data.Data as HoursLogModel).AppModel;

        var categories = categorys.GetCategories();
        var categoryId = appData.GetApp(app.ID).CategoryID;
        foreach (var category in categories)
        {
            var categoryMenu = new MenuItem();
            categoryMenu.ToggleType = MenuItemToggleType.Radio;
            categoryMenu.Header = category.Name;
            categoryMenu.IsChecked = categoryId == category.ID;
            categoryMenu.IsSelected = true;
            categoryMenu.Click += (s, e) => { SetAppCategory(data, app.ID, category); };
            setCategory.Items.Add(categoryMenu);
        }

        if (categoryId != 0)
        {
            setCategory.Items.Add(new Separator());
            var un = new MenuItem();
            un.Header = ResourceStrings.Uncategorized;
            un.Click += (s, e) => { ClearCategory(app.ID); };
            setCategory.Items.Add(un);
        }

        setCategory.IsEnabled = setCategory.Items.Count > 0;
    }

    private void ClearCategory(int appId)
    {
        var app = appData.GetApp(appId);
        app.CategoryID = 0;
        app.Category = null;
        appData.UpdateApp(app);
    }


    private void UpdateLinks()
    {
        setLink.Items.Clear();

        var data = menu.Tag as ChartsDataModel;
        var log = data.Data as DailyLogModel;
        var app = log != null ? log.AppModel : null;

        if (log == null) app = (data.Data as HoursLogModel).AppModel;
        var config = appConfig.GetConfig();

        var links = config.Links;
        foreach (var link in links)
        {
            var categoryMenu = new MenuItem();
            categoryMenu.Header = link.Name;
            categoryMenu.Click += (s, e) => { SetLink(app, link.Name); };
            setLink.Items.Add(categoryMenu);
        }
    }


    private void SetLink(AppModel app, string linkName)
    {
        var config = appConfig.GetConfig();
        var links = config.Links;
        var link = links.Where(m => m.ProcessList.Contains(app.Name)).FirstOrDefault();
        if (link != null) link.ProcessList.Remove(app.Name);

        link = links.Where(m => m.Name == linkName).FirstOrDefault();
        if (link != null)
        {
            link.ProcessList.Add(app.Name);
            appConfig.Save();

            main.Toast(ResourceStrings.AssociationSuccessful, ToastType.Success);
        }
        else
        {
            main.Toast(ResourceStrings.AssociationConfigurationNotExist, ToastType.Error, IconTypes.IncidentTriangle);
        }
    }

    private void SetAppCategory(ChartsDataModel data, int appId, CategoryModel category)
    {
        var newBadgeList = new List<ChartBadgeModel>();
        if (data.BadgeList != null)
        {
            var otherBadge = data.BadgeList.Where(m => m.Type != ChartBadgeType.Category).ToList();
            newBadgeList.AddRange(otherBadge);
        }

        newBadgeList.Add(new ChartBadgeModel
        {
            Name = category.Name,
            Color = category.Color,
            Type = ChartBadgeType.Category
        });

        data.BadgeList = newBadgeList;

        var app = appData.GetApp(appId);
        app.CategoryID = category.ID;
        app.Category = category;
        appData.UpdateApp(app);
    }


    private void OpenDir_Click(object sender, PointerPressedEventArgs e)
    {
        var data = menu.Tag as ChartsDataModel;

        var log = data.Data as DailyLogModel;
        var app = log != null ? log.AppModel : null;

        if (log == null) app = (data.Data as HoursLogModel).AppModel;

        if (File.Exists(app.File))
            Process.Start("explorer.exe", "/select, " + app.File);
        else
            main.Toast(ResourceStrings.ApplicationFileExist, ToastType.Error, IconTypes.IncidentTriangle);
    }

    private void Run_Click(object sender, PointerPressedEventArgs e)
    {
        var data = menu.Tag as ChartsDataModel;

        var log = data.Data as DailyLogModel;
        var app = log != null ? log.AppModel : null;

        if (log == null) app = (data.Data as HoursLogModel).AppModel;

        if (File.Exists(app.File))
        {
            Process.Start(app.File);
            main.Toast(ResourceStrings.OperationCompleted);
        }
        else
        {
            main.Toast(ResourceStrings.ApplicationFileExist, ToastType.Error, IconTypes.IncidentTriangle);
        }
    }


    private void _whiteList_Click(object sender, PointerPressedEventArgs e)
    {
        var data = menu.Tag as ChartsDataModel;
        var log = data.Data as DailyLogModel;
        var app = log != null ? log.AppModel : null;

        if (log == null) app = (data.Data as HoursLogModel).AppModel;
        var config = appConfig.GetConfig();
        if (config.Behavior.ProcessWhiteList.Contains(app.Name))
        {
            config.Behavior.ProcessWhiteList.Remove(app.Name);
            main.Toast($"{ResourceStrings.RemovedApplicationFromWhitelist} {app.Description}", ToastType.Success);
        }
        else
        {
            config.Behavior.ProcessWhiteList.Add(app.Name);
            main.Toast($"{ResourceStrings.AddedToWhitelist} {app.Description}", ToastType.Success);
        }
    }
}
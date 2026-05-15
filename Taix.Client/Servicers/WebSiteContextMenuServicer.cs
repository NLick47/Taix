using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Taix.Client.Controls.Charts.Model;
using Taix.Client.Controls.Window;
using Taix.Client.Logging;
using Taix.Client.Shared.Helpers;
using Taix.Client.Shared.Models.Db;
using Taix.Client.Shared.Servicers.Interfaces;
using Taix.Client.ViewModels;

namespace Taix.Client.Servicers;

public class WebSiteContextMenuServicer : IWebSiteContextMenuServicer, IDisposable
{
    private readonly IAppConfig _appConfig;


    private readonly MainViewModel _main;
    private readonly IThemeServicer _theme;
    private readonly IUIServicer _uIServicer;
    private readonly IWebData _webData;
    private MenuItem _block;
    private MenuItem _editAlias;

    private ContextMenu _menu;
    private MenuItem _open;
    private MenuItem _setCategory;
    private MenuItem _site;
    private IDisposable? _languageSubscription;

    public WebSiteContextMenuServicer(
        MainViewModel main,
        IAppConfig appConfig,
        IThemeServicer theme,
        IWebData webData,
        IUIServicer uiServicer)
    {
        _main = main;
        _appConfig = appConfig;
        _theme = theme;
        _webData = webData;
        _uIServicer = uiServicer;
    }

    public void Init()
    {
        InitializeMenuItems();
        _languageSubscription = _appConfig.WhenLanguageChanged(UpdateMenuTexts);
    }

    public void Dispose()
    {
        _languageSubscription?.Dispose();
    }

    public ContextMenu GetContextMenu()
    {
        return _menu;
    }


    private void UpdateMenuTexts()
    {
        _open.Header = ResourceStrings.OpenWebsite;
        _setCategory.Header = ResourceStrings.SetCategory;
        _editAlias.Header = ResourceStrings.EditAlias;
        _block.Header = ResourceStrings.IgnoreSite;
    }

    private void InitializeMenuItems()
    {
        if (_menu != null)
        {
            _menu.Opening -= _menu_ContextMenuOpening;
            _menu.Items.Clear();
        }

        _menu = new ContextMenu();
        _open = new MenuItem();
        _open.PointerPressed += Open_Click;
        ;
        _setCategory = new MenuItem();

        _editAlias = new MenuItem();

        _editAlias.Click += EditAlias_ClickAsync;

        _block = new MenuItem();

        _block.PointerPressed += Block_Click;

        _site = new MenuItem();
        _site.IsEnabled = false;

        _menu.Items.Add(_site);
        _menu.Items.Add(_open);
        _menu.Items.Add(new Separator());
        _menu.Items.Add(_setCategory);
        _menu.Items.Add(_editAlias);
        _menu.Items.Add(new Separator());
        _menu.Items.Add(_block);
        _menu.Opening += _menu_ContextMenuOpening;
        UpdateMenuTexts();
    }

    private async void EditAlias_ClickAsync(object? sender, RoutedEventArgs e)
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
        var data = _menu.Tag as ChartsDataModel;
        var site = data.Data as WebSiteModel;

        var input = await _uIServicer.ShowInputModalAsync(ResourceStrings.EditAlias, ResourceStrings.EnterAlias,
            site.Alias, val =>
            {
                if (val?.Length > 15)
                {
                    _main.Error(string.Format(ResourceStrings.AliasMaxLengthTip, 15));
                    return false;
                }

                return true;
            });

        data.Name = string.IsNullOrEmpty(input) ? site.Title : input;
        site = site with { Alias = input };

        await _webData.UpdateAsync(site);

        _main.Success(ResourceStrings.AliasUpdated);
    }

    private async void _menu_ContextMenuOpening(object? sender, CancelEventArgs e)
    {
        try
        {
            await _menu_ContextMenuOpeningAsync();
        }
        catch (Exception ex)
        {
            Logger.Error($"网站上下文菜单打开失败: {ex.Message}", ex);
        }
    }

    private async Task _menu_ContextMenuOpeningAsync()
    {
        if (_menu.Tag == null) return;
        var data = _menu.Tag as ChartsDataModel;
        var site = data.Data as WebSiteModel;
        _site.Header = site.Title;

        var config = _appConfig.GetConfig();
        if (config.Behavior.IgnoreUrlList.Contains(site.Domain))
            _block.Header = ResourceStrings.UnignoreSite;
        else
            _block.Header = ResourceStrings.IgnoreSite;

        await UpdateCategoryMenuAsync();
    }

    private void Open_Click(object? sender, PointerPressedEventArgs e)
    {
        var data = _menu.Tag as ChartsDataModel;
        var site = data.Data as WebSiteModel;
        if (!string.IsNullOrEmpty(site.Domain))
        {
            _main.Info(ResourceStrings.OperationCompleted);
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = $"http://{site.Domain}",
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                Logger.Error("打开网址链接" + ex);
            }
        }
    }


    private void Block_Click(object? sender, PointerPressedEventArgs e)
    {
        var data = _menu.Tag as ChartsDataModel;
        var site = data.Data as WebSiteModel;
        if (site == null) return;

        var newBadgeList = new List<ChartBadgeModel>();
        if (data.BadgeList != null)
        {
            var categoryBadge = data.BadgeList.Where(m => m.Type != ChartBadgeType.Ignore).ToList();
            newBadgeList.AddRange(categoryBadge);
        }

        var config = _appConfig.GetConfig();
        if (config.Behavior.IgnoreUrlList.Contains(site.Domain))
        {
            config.Behavior.IgnoreUrlList.Remove(site.Domain);
            _main.Toast(string.Format(ResourceStrings.UnignoredDomain, site.Domain), ToastType.Success);
        }
        else
        {
            config.Behavior.IgnoreUrlList.Add(site.Domain);
            _main.Toast(string.Format(ResourceStrings.IgnoredDomain, site.Domain), ToastType.Success);

            newBadgeList.Add(ChartBadgeModel.IgnoreBadge);
        }

        data.BadgeList = newBadgeList;
    }


    private async Task UpdateCategoryMenuAsync()
    {
        _setCategory.Items.Clear();

        var data = _menu.Tag as ChartsDataModel;
        var site = data.Data as WebSiteModel;
        var categories = await _webData.GetWebSiteCategoriesAsync();
        var siteFromCache = await _webData.GetWebSiteAsync(site.ID);
        var siteCategoryId = siteFromCache?.CategoryID ?? 0;
        foreach (var category in categories)
        {
            if (category.IsSystem) continue;
            var categoryMenu = new MenuItem();
            categoryMenu.ToggleType = MenuItemToggleType.Radio;
            categoryMenu.IsChecked = siteCategoryId == category.ID;
            categoryMenu.Header = category.Name;
            categoryMenu.Click += async (s, e) => await UpdateSiteCategoryAsync(data, category.ID);
            _setCategory.Items.Add(categoryMenu);
        }

        var sysCategory = categories.FirstOrDefault(c => c.IsSystem);
        if (siteCategoryId != 0 && sysCategory != null && siteCategoryId != sysCategory.ID)
        {
            _setCategory.Items.Add(new Separator());
            var uncategorizedMenuItem = new MenuItem
            {
                Header = sysCategory?.Name ?? ResourceStrings.Uncategorized
            };
            uncategorizedMenuItem.Click += async (s, e) => await ClearSiteCategoryAsync();
            _setCategory.Items.Add(uncategorizedMenuItem);
        }
    }

    private async Task ClearSiteCategoryAsync()
    {
        var data = _menu.Tag as ChartsDataModel;
        if (data != null)
        {
            await _webData.UpdateWebSitesCategoryAsync(new[] { (data.Data as WebSiteModel).ID }, 0);
            data.BadgeList = new List<ChartBadgeModel>();
        }
    }


    private async Task UpdateSiteCategoryAsync(ChartsDataModel data, int categoryId)
    {
        var category = await _webData.GetWebSiteCategoryAsync(categoryId);
        if (category != null)
        {
            var site = data.Data as WebSiteModel;
            await _webData.UpdateWebSitesCategoryAsync(new[] { site.ID }, categoryId);
            site = site with { CategoryID = categoryId, Category = category };

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
        }
    }
}

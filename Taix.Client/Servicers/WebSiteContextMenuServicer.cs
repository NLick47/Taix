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
    private readonly IUIServicer _uIServicer;
    private readonly IWebData _webData;
    private MainViewModel? _mainViewModel;
    private MenuItem _block;
    private MenuItem _editAlias;

    private ContextMenu _menu;
    private MenuItem _open;
    private MenuItem _setCategory;
    private MenuItem _site;
    private IDisposable? _languageSubscription;

    public WebSiteContextMenuServicer(
        IAppConfig appConfig,
        IWebData webData,
        IUIServicer uiServicer)
    {
        _appConfig = appConfig;
        _webData = webData;
        _uIServicer = uiServicer;
    }

    public void Init()
    {
        _languageSubscription = _appConfig.WhenLanguageChanged(UpdateMenuTexts);
        _mainViewModel = ServiceLocator.GetService<MainViewModel>();
    }

    public void Dispose()
    {
        _languageSubscription?.Dispose();
    }

    public ContextMenu GetContextMenu()
    {
        if (_menu == null)
        {
            InitializeMenuItems();
        }
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
                    _mainViewModel?.Error(string.Format(ResourceStrings.AliasMaxLengthTip, 15));
                    return false;
                }

                return true;
            });

        data.Name = string.IsNullOrEmpty(input) ? site.Title : input;
        site = site with { Alias = input };

        await _webData.UpdateAsync(site);

        _mainViewModel?.Success(ResourceStrings.AliasUpdated);
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
            _mainViewModel?.Info(ResourceStrings.OperationCompleted);
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


    private async void Block_Click(object? sender, PointerPressedEventArgs e)
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
            _mainViewModel?.Toast(string.Format(ResourceStrings.UnignoredDomain, site.Domain), ToastType.Success);
        }
        else
        {
            config.Behavior.IgnoreUrlList.Add(site.Domain);
            _mainViewModel?.Toast(string.Format(ResourceStrings.IgnoredDomain, site.Domain), ToastType.Success);

            newBadgeList.Add(ChartBadgeModel.IgnoreBadge);
        }

        data.BadgeList = newBadgeList;

        await _appConfig.SaveAsync();
    }


    private async Task UpdateCategoryMenuAsync()
    {
        _setCategory.Items.Clear();

        var data = _menu.Tag as ChartsDataModel;
        var site = data.Data as WebSiteModel;

        var categories = await _webData.GetWebSiteCategoriesAsync();

        var siteCategoryId = site?.CategoryID ?? 0;
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

        var currentCategory = categories.FirstOrDefault(c => c.ID == siteCategoryId);
        if (currentCategory != null && !currentCategory.IsSystem)
        {
            _setCategory.Items.Add(new Separator());
            var sysCategory = categories.FirstOrDefault(c => c.IsSystem);
            var uncategorizedMenuItem = new MenuItem
            {
                Header = sysCategory?.Name ?? ResourceStrings.Uncategorized
            };
            uncategorizedMenuItem.Click += async (s, e) => await ClearSiteCategoryAsync();
            _setCategory.Items.Add(uncategorizedMenuItem);
        }

        // 添加新建分类选项
        _setCategory.Items.Add(new Separator());
        var newCategoryMenuItem = new MenuItem
        {
            Header = ResourceStrings.NewCategory
        };
        newCategoryMenuItem.Click += async (s, e) => await CreateNewCategoryAndAssignSiteAsync(categories);
        _setCategory.Items.Add(newCategoryMenuItem);
    }

    private async Task ClearSiteCategoryAsync()
    {
        var data = _menu.Tag as ChartsDataModel;
        if (data != null)
        {
            var site = data.Data as WebSiteModel;
            await _webData.UpdateWebSitesCategoryAsync(new[] { site.ID }, 0);
            data.Data = site with { CategoryID = 0, Category = null };
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
            data.Data = site with { CategoryID = categoryId, Category = category };

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

    private async Task CreateNewCategoryAndAssignSiteAsync(IReadOnlyList<WebSiteCategoryModel> existingCategories)
    {
        var data = _menu.Tag as ChartsDataModel;
        if (data?.Data is not WebSiteModel site) return;

        var existingNames = existingCategories.Select(c => c.Name);

        var result = await _uIServicer.ShowCreateCategoryDialogAsync(
            ResourceStrings.NewCategory,
            null,
            existingNames);

        if (result == null) return;

        // 检查颜色是否已存在（网站分类需要唯一颜色）
        if (existingCategories.Any(c => c.Color?.Equals(result.Color, StringComparison.OrdinalIgnoreCase) == true))
        {
            _mainViewModel?.Error(ResourceStrings.ColoreExists);
            return;
        }

        // 创建新分类
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

        // 自动将网站分配到新分类
        await UpdateSiteCategoryAsync(data, newCategory.ID);

        _mainViewModel?.Success(ResourceStrings.CategoryCreated);
    }
}

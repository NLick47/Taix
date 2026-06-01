using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Threading;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ReactiveUI;
using Taix.Client.Controls.Charts.Model;
using Taix.Client.Controls.Select;
using Taix.Client.Models;
using Taix.Client.Servicers.Interfaces;
using Taix.Client.Shared.Helpers;
using Taix.Client.Shared.Librarys;
using Taix.Client.Shared.Models.Db;
using Taix.Client.Shared.Servicers.Interfaces;

namespace Taix.Client.ViewModels;

public class WebSiteDetailPageViewModel : WebSiteDetailPageModel
{
    private readonly IAppConfig _appConfig;
    private readonly IClipboardService _clipboardService;
    private readonly IDialogService _dialogService;
    private IDisposable? _languageSubscription;
    private readonly IProcessService _processService;
    private readonly IToastService _toastService;
    private readonly INavigationDataService _navigationData;
    private readonly IWebData _webData;

    private MenuItem _blockMenuItem = new();
    private MenuItem _clearMenuItem = new();
    private MenuItem _copyDomainMenuItem = new();
    private MenuItem _editAliasMenuItem = new();
    private MenuItem _openMenuItem = new();
    private MenuItem _reloadDataMenuItem = new();
    private MenuItem _setCategoryMenuItem = new();

    public WebSiteDetailPageViewModel(
        IWebData webData,
        INavigationDataService navigationData,
        IAppConfig appConfig,
        IDialogService dialogService,
        IClipboardService clipboardService,
        IProcessService processService,
        IToastService toastService)
    {
        _webData = webData;
        _navigationData = navigationData;
        _appConfig = appConfig;
        _dialogService = dialogService;
        _clipboardService = clipboardService;
        _processService = processService;
        _toastService = toastService;

        Initialize();
    }

    public ReactiveCommand<object, Unit> PageCommand { get; private set; }

    private void Initialize()
    {
        if (_navigationData.Data is not WebSiteModel webSite)
        {
            _toastService.Error(ResourceStrings.InvalidParameter);
            return;
        }

        WebSite = webSite;

        TabbarData = [ResourceStrings.Daily, ResourceStrings.Weekly, ResourceStrings.Monthly, ResourceStrings.Yearly];
        PeriodOptions =
        [
            new SelectItemModel { Id = 0, Name = ResourceStrings.Daily },
            new SelectItemModel { Id = 1, Name = ResourceStrings.Weekly },
            new SelectItemModel { Id = 2, Name = ResourceStrings.Monthly },
            new SelectItemModel { Id = 3, Name = ResourceStrings.Yearly }
        ];

        ChartDate = DateTime.Now;
        WeekDate = DateTime.Now;
        SelectedPeriod = PeriodOptions[0];
        MonthDate = DateTime.Now;
        YearDate = DateTime.Now;
        TabbarSelectedIndex = 0;

        _languageSubscription = _appConfig.WhenLanguageChanged(UpdateMenuTexts);

        WhenPropertyChanged(this, x => x.TabbarSelectedIndex, _ => LoadDataAsync());
        WhenPropertyChanged(this, x => x.ChartDate, _ => LoadDataAsync());
        WhenPropertyChanged(this, x => x.WeekDate, _ => LoadDataAsync());
        WhenPropertyChanged(this, x => x.MonthDate, _ => LoadDataAsync());
        WhenPropertyChanged(this, x => x.YearDate, _ => LoadDataAsync());
        WhenPropertyChanged(this, x => x.SelectedPeriod, p =>
        {
            if (p != null) TabbarSelectedIndex = p.Id;
            return Task.CompletedTask;
        });
        WhenPropertyChanged(this, x => x.Category, async category =>
        {
            if (category == null || WebSite == null) return;
            if (category.Id == WebSite.CategoryID) return;
            await UpdateCategoryAsync(category.Id);
        });

        InitializeMenuItems();
        UpdateMenuTexts();

        PageCommand = ReactiveCommand.CreateFromTask<object>(OnPageCommandAsync);
        PageCommand.DisposeWith(Disposables);
    }

    public override Task OnNavigatedToAsync()
    {
        _ = ExecuteAsync(LoadDataCoreAsync);
        return Task.CompletedTask;
    }

    private Task LoadDataAsync() => ExecuteAsync(LoadDataCoreAsync);

    private async Task LoadDataCoreAsync(CancellationToken cancellationToken)
    {
        if (WebSite == null) return;
        IsIgnore = IsUrlIgnore(WebSite.Domain);

        var startDate = DateTime.Now;
        var endDate = DateTime.Now;
        var colNames = Array.Empty<string>();
        NameIndexStart = 0;

        if (TabbarSelectedIndex == 0)
        {
            startDate = endDate = ChartDate;
        }
        else if (TabbarSelectedIndex == 1)
        {
            NameIndexStart = 0;
            var weekDateArr = GetWeekRange(WeekDate);

            startDate = weekDateArr.Start;
            endDate = weekDateArr.End;
            colNames =
            [
                ResourceStrings.Monday, ResourceStrings.Tuesday, ResourceStrings.Wednesday, ResourceStrings.Thursday,
                ResourceStrings.Friday, ResourceStrings.Saturday, ResourceStrings.Sunday
            ];
        }
        else if (TabbarSelectedIndex == 2)
        {
            var dateArr = Time.GetMonthDate(MonthDate);
            startDate = dateArr[0];
            endDate = dateArr[1];
            NameIndexStart = 1;
        }
        else if (TabbarSelectedIndex == 3)
        {
            NameIndexStart = 1;
            startDate = new DateTime(YearDate.Year, 1, 1);
            endDate = new DateTime(YearDate.Year, 12, DateTime.DaysInMonth(YearDate.Year, 12), 23, 59, 59);

            colNames = new string[12];
            for (var i = 0; i < 12; i++)
                colNames[i] = Application.Current?.Resources[$"{i + 1}Month"] as string ?? $"{i + 1}";
        }

        cancellationToken.ThrowIfCancellationRequested();

        var list = await _webData.GetBrowseDataStatisticsAsync(startDate, endDate, WebSite.ID, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        ChartData =
        [
            new ChartsDataModel
            {
                Name = !string.IsNullOrEmpty(WebSite.Alias) ? WebSite.Alias : WebSite.Title,
                Values = list.Select(m => m.Value).ToArray(),
                ColumnNames = colNames,
                Color = StateData.ThemeColor
            }
        ];

        WebPageData = (await _webData.GetBrowseLogListAsync(startDate, endDate, WebSite.ID))
            .OrderByDescending(m => m.ID)
            .ToList();
        cancellationToken.ThrowIfCancellationRequested();

        await LoadCategoriesAsync(cancellationToken);
    }

    private async Task LoadCategoriesAsync(CancellationToken cancellationToken)
    {
        if (WebSite == null) return;
        var data = await _webData.GetWebSiteCategoriesAsync(cancellationToken);
        var list = new List<SelectItemModel>();
        foreach (var category in data)
        {
            list.Add(new SelectItemModel
            {
                Name = category.Name,
                Img = category.IconFile,
                Id = category.ID,
                Data = category
            });
        }

        cancellationToken.ThrowIfCancellationRequested();

        Categories = list;
        Category = Categories.FirstOrDefault(m => m.Id == WebSite.CategoryID);
    }

    private async Task UpdateCategoryAsync(int categoryId)
    {
        if (WebSite == null) return;
        await _webData.UpdateWebSitesCategoryAsync(new[] { WebSite.ID }, categoryId);
        WebSite = WebSite with { CategoryID = categoryId };
        if (Category == null || categoryId != Category.Id)
            Category = Categories.FirstOrDefault(m => m.Id == WebSite.CategoryID);
    }

    private async Task OnPageCommandAsync(object obj)
    {
        if (WebPageSelectedItem?.Url == null) return;

        var url = WebPageSelectedItem.Url.Url;
        if (string.IsNullOrEmpty(url)) return;
        if (!url.Contains("://")) url = "http://" + url;

        switch (obj.ToString())
        {
            case "Open":
                _processService.OpenUrl(url);
                break;
            case "CopyURL":
                await _clipboardService.SetTextAsync(url);
                break;
            case "CopyTitle":
                if (WebPageSelectedItem.Url.Title != null)
                    await _clipboardService.SetTextAsync(WebPageSelectedItem.Url.Title);
                break;
        }
    }

    private void InitializeMenuItems()
    {
        WebSiteContextMenu = new ContextMenu();
        WebSiteContextMenu.Opened += OnWebSiteContextMenuOpened;

        _openMenuItem = new MenuItem();
        var openCommand = ReactiveCommand.Create(() =>
        {
            if (!string.IsNullOrEmpty(WebSite!.Domain))
                _processService.OpenUrl(WebSite.Domain);
        });
        openCommand.DisposeWith(Disposables);
        _openMenuItem.Command = openCommand;

        _copyDomainMenuItem = new MenuItem();
        var copyCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            if (!string.IsNullOrEmpty(WebSite!.Domain))
                await _clipboardService.SetTextAsync(WebSite.Domain);
        });
        copyCommand.DisposeWith(Disposables);
        _copyDomainMenuItem.Command = copyCommand;

        _reloadDataMenuItem = new MenuItem();
        var reloadCommand = ReactiveCommand.CreateFromTask(LoadDataAsync);
        reloadCommand.DisposeWith(Disposables);
        _reloadDataMenuItem.Command = reloadCommand;

        _clearMenuItem = new MenuItem();
        var clearCommand = ReactiveCommand.CreateFromTask(OnClearAsync);
        clearCommand.DisposeWith(Disposables);
        _clearMenuItem.Command = clearCommand;

        _setCategoryMenuItem = new MenuItem();

        _editAliasMenuItem = new MenuItem();
        var editCommand = ReactiveCommand.CreateFromTask(OnEditAliasAsync);
        editCommand.DisposeWith(Disposables);
        _editAliasMenuItem.Command = editCommand;

        _blockMenuItem = new MenuItem();
        var blockCommand = ReactiveCommand.Create(OnBlockAction);
        blockCommand.DisposeWith(Disposables);
        _blockMenuItem.Command = blockCommand;

        WebSiteContextMenu.Items.Add(_openMenuItem);
        WebSiteContextMenu.Items.Add(_reloadDataMenuItem);
        WebSiteContextMenu.Items.Add(_copyDomainMenuItem);
        WebSiteContextMenu.Items.Add(new Separator());
        WebSiteContextMenu.Items.Add(_setCategoryMenuItem);
        WebSiteContextMenu.Items.Add(_editAliasMenuItem);
        WebSiteContextMenu.Items.Add(new Separator());
        WebSiteContextMenu.Items.Add(_blockMenuItem);
        WebSiteContextMenu.Items.Add(_clearMenuItem);
    }

    private void OnWebSiteContextMenuOpened(object? sender, RoutedEventArgs e)
    {
        _ = RefreshMenuItemsAsync();
    }

    private async Task RefreshMenuItemsAsync()
    {
        if (WebSite == null) return;
        _setCategoryMenuItem.Items.Clear();

        var webSite = await _webData.GetWebSiteAsync(WebSite.ID);
        if (webSite == null) return;
        var categoryId = webSite.CategoryID;

        var sysCategories = Categories.Where(m => m.Data is WebSiteCategoryModel wc && wc.IsSystem).ToList();
        var userCategories = Categories.Where(m => m.Data is WebSiteCategoryModel wc && !wc.IsSystem).ToList();

        // 用户分类
        foreach (var category in userCategories)
        {
            var categoryMenu = new MenuItem
            {
                Header = category.Name,
                ToggleType = MenuItemToggleType.Radio,
                IsChecked = categoryId == category.Id
            };
            var command = ReactiveCommand.Create(() => _ = UpdateCategoryAsync(category.Id));
            categoryMenu.Command = command;
            _setCategoryMenuItem.Items.Add(categoryMenu);
        }

        var sysCategory = sysCategories.FirstOrDefault();
        var sysCategoryModel = sysCategory?.Data as WebSiteCategoryModel;
        if (categoryId != 0 && sysCategoryModel != null && categoryId != sysCategoryModel.ID)
        {
            if (userCategories.Count > 0)
            {
                _setCategoryMenuItem.Items.Add(new Separator());
            }
            var un = new MenuItem
            {
                Header = sysCategory?.Name ?? ResourceStrings.Uncategorized,
                Command = ReactiveCommand.Create(() => _ = ClearSiteCategoryAsync())
            };
            _setCategoryMenuItem.Items.Add(un);
        }

        _blockMenuItem.Header = IsIgnore ? ResourceStrings.Unignore : ResourceStrings.IgnoreTheSite;
        _blockMenuItem.IsEnabled = !IsUrlRegexIgnore(WebSite.Domain);
    }

    private void UpdateMenuTexts()
    {
        _openMenuItem.Header = ResourceStrings.OpenWebsite;
        _copyDomainMenuItem.Header = ResourceStrings.CopyDomain;
        _reloadDataMenuItem.Header = ResourceStrings.Refresh;
        _clearMenuItem.Header = ResourceStrings.ClearStatistics;
        _editAliasMenuItem.Header = ResourceStrings.EditAlias;
        _setCategoryMenuItem.Header = ResourceStrings.SetCategory;
        _blockMenuItem.Header = ResourceStrings.IgnoreWebsite;
    }

    private async Task OnEditAliasAsync()
    {
        if (WebSite == null) return;
        try
        {
            var input = await _dialogService.ShowInputModalAsync(
                ResourceStrings.EditAlias,
                ResourceStrings.EnterAlias,
                WebSite.Alias,
                val =>
                {
                    if (val?.Length > 15)
                    {
                        _toastService.Error(string.Format(ResourceStrings.AliasMaxLengthTip, 15));
                        return false;
                    }
                    return true;
                });

            WebSite = WebSite with { Alias = input };
            var updated = await _webData.UpdateAsync(WebSite);
            if (updated != null) WebSite = updated;
            _toastService.Success(ResourceStrings.AliasUpdated);
        }
        catch
        {
            // 输入取消，无需处理
        }
    }

    private async Task OnClearAsync()
    {
        if (WebSite == null) return;
        var isConfirm = await _dialogService.ShowConfirmDialogAsync(
            ResourceStrings.ClearConfirmation,
            ResourceStrings.ClearAllStatisticsSiteTip);

        if (isConfirm)
        {
            await _webData.ClearAsync(WebSite.ID);
            await ExecuteAsync(LoadDataCoreAsync, trackLoading: false);
            _toastService.Success(ResourceStrings.OperationCompleted);
        }
    }

    private void OnBlockAction()
    {
        if (WebSite == null) return;
        var config = _appConfig.GetConfig();

        if (IsIgnore)
            config.Behavior.IgnoreUrlList.Remove(WebSite.Domain);
        else
            config.Behavior.IgnoreUrlList.Add(WebSite.Domain);

        IsIgnore = !IsIgnore;
        _toastService.Success(ResourceStrings.OperationCompleted);
    }

    private async Task ClearSiteCategoryAsync()
    {
        if (WebSite == null) return;
        await _webData.UpdateWebSitesCategoryAsync(new[] { WebSite.ID }, 0);
        Category = null;
    }

    private bool IsUrlIgnore(string url)
    {
        var ignoreUrlList = _appConfig.GetConfig().Behavior.IgnoreUrlList;
        return ignoreUrlList.Any(item => IsUrlMatch(url, item));
    }

    private bool IsUrlRegexIgnore(string url)
    {
        var ignoreUrlList = _appConfig.GetConfig().Behavior.IgnoreUrlList;
        return ignoreUrlList.Any(item => IsRegexPattern(item) && RegexHelper.IsMatch(url, item));
    }

    private static bool IsUrlMatch(string url, string pattern)
    {
        if (IsRegexPattern(pattern))
            return RegexHelper.IsMatch(url, pattern);
        return url.Contains(pattern);
    }

    private static bool IsRegexPattern(string pattern)
    {
        if (string.IsNullOrEmpty(pattern))
            return false;
        return Regex.IsMatch(pattern, @"[\.\*\?\{\\\[\^\|]");
    }

    private static (DateTime Start, DateTime End) GetWeekRange(DateTime date)
    {
        var dayOfWeek = (int)date.DayOfWeek;
        dayOfWeek = dayOfWeek == 0 ? 7 : dayOfWeek;
        var start = date.Date.AddDays(-(dayOfWeek - 1));
        var end = start.AddDays(6).Date.AddHours(23).AddMinutes(59).AddSeconds(59);
        return (start, end);
    }

    public override void Dispose()
    {
        _languageSubscription?.Dispose();
        if (WebSiteContextMenu != null)
            WebSiteContextMenu.Opened -= OnWebSiteContextMenuOpened;

        base.Dispose();
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Threading;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using ReactiveUI;
using Taix.Client.Controls.Charts;
using Taix.Client.Controls.Charts.Model;
using Taix.Client.Controls.Select;
using Taix.Client.Events;
using Taix.Client.Models;
using Taix.Client.Models.Navigation;
using Taix.Client.Servicers;
using Taix.Client.Servicers.Interfaces;
using Taix.Client.Shared.Helpers;
using Taix.Client.Shared.Librarys;
using Taix.Client.Shared.Models.Web;
using Taix.Client.Shared.Servicers.Interfaces;

namespace Taix.Client.ViewModels;

public class WebSiteDetailPageViewModel : WebSiteDetailPageModel
{
    private readonly IAppConfig _appConfig;
    private readonly IClipboardService _clipboardService;
    private readonly IDialogService _dialogService;
    private readonly IProcessService _processService;
    private readonly IToastService _toastService;
    private readonly INavigationDataService _navigationData;
    private readonly IWebData _webData;
    private readonly IContextMenuServicer _contextMenuService;
    private readonly IAppEventService _appEventService;
    private IDisposable? _languageSubscription;

    public WebSiteDetailPageViewModel(
        IWebData webData,
        INavigationDataService navigationData,
        IAppConfig appConfig,
        IDialogService dialogService,
        IClipboardService clipboardService,
        IProcessService processService,
        IToastService toastService,
        IContextMenuServicer contextMenuService,
        IAppEventService appEventService)
    {
        _webData = webData;
        _navigationData = navigationData;
        _appConfig = appConfig;
        _dialogService = dialogService;
        _clipboardService = clipboardService;
        _processService = processService;
        _toastService = toastService;
        _contextMenuService = contextMenuService;
        _appEventService = appEventService;

        PageCommand = ReactiveCommand.CreateFromTask<object>(OnPageCommandAsync);
        PageCommand.DisposeWith(Disposables);

        Initialize();
    }

    public ReactiveCommand<object, Unit> PageCommand { get; private set; }

    private void Initialize()
    {
        TabbarData = [ResourceStrings.Daily, ResourceStrings.Weekly, ResourceStrings.Monthly, ResourceStrings.Yearly];
        PeriodOptions =
        [
            new SelectItemModel { Id = 0, Name = ResourceStrings.Daily },
            new SelectItemModel { Id = 1, Name = ResourceStrings.Weekly },
            new SelectItemModel { Id = 2, Name = ResourceStrings.Monthly },
            new SelectItemModel { Id = 3, Name = ResourceStrings.Yearly }
        ];

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
    }

    public override async Task OnNavigatedToAsync()
    {
        WebSiteModel? webSite = null;
        int periodType = 0;
        DateTime date = DateTime.Now;

        if (_navigationData.Data is WebSiteDetailNavigationContext context)
        {
            webSite = context.WebSite;
            periodType = context.PeriodType;
            date = context.Date;
        }
        else if (_navigationData.Data is WebSiteModel model)
        {
            webSite = model;
        }

        if (webSite == null)
        {
            _toastService.Error(ResourceStrings.InvalidParameter);
            return;
        }

        IsRestoringState = true;
        WebSite = webSite;

        _appEventService.WebSiteChanged
            .Where(e => e.WebSite.ID == WebSite?.ID)
            .Subscribe(e => OnWebSiteChanged(e.WebSite, e.ChangeType))
            .DisposeWith(Disposables);

        ChartDate = date;
        WeekDate = date;
        MonthDate = date;
        YearDate = date;
        TabbarSelectedIndex = periodType;
        SelectedPeriod = PeriodOptions[periodType];
        IsRestoringState = false;

        // 设置 ContextMenu 的数据
        await UpdateContextMenuDataAsync();

        await ExecuteAsync(LoadDataCoreAsync);
    }

    /// <summary>
    /// 更新 ContextMenu 的数据上下文
    /// </summary>
    private async Task UpdateContextMenuDataAsync()
    {
        if (WebSite == null) return;

        var chartData = new ChartsDataModel
        {
            Name = WebSite.Title,
            Data = WebSite
        };
        WebSiteContextMenu = await _contextMenuService.CreateContextMenuAsync(ContextMenuType.WebSiteDetail, chartData);
    }

    private void UpdateMenuTexts()
    {
    }

    private void OnWebSiteChanged(WebSiteModel updatedSite, AppChangeType changeType)
    {
        WebSite = updatedSite;

        if (changeType.HasFlag(AppChangeType.WebSiteCategory))
        {
            _ = ReloadCategoryAndRefreshAsync(updatedSite.CategoryID);
        }
        else
        {
            _ = UpdateContextMenuDataAsync();
        }
    }

    private async Task ReloadCategoryAndRefreshAsync(int categoryId)
    {
        await LoadCategoriesAsync(CancellationToken.None);
        await UpdateContextMenuDataAsync();
        await LoadDataAsync();
    }

    private Task LoadDataAsync() => ExecuteAsync(LoadDataCoreAsync);

    public override Task RefreshAsync() => LoadDataAsync();

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

        WebSiteCategoryModel? category;
        if (categoryId > 0)
        {
            var categories = await _webData.GetWebSiteCategoriesAsync();
            category = categories.FirstOrDefault(c => c.ID == categoryId);
        }
        else
        {
            var categories = await _webData.GetWebSiteCategoriesAsync();
            category = categories.FirstOrDefault(c => c.IsSystem);
            categoryId = category?.ID ?? 0;
        }

        await _webData.UpdateWebSitesCategoryAsync(new[] { WebSite.ID }, categoryId);
        WebSite = WebSite with { CategoryID = categoryId, Category = category };
        if (Category == null || categoryId != Category.Id)
            Category = Categories.FirstOrDefault(m => m.Id == categoryId);
        await UpdateContextMenuDataAsync();
        if (WebSite != null)
            _appEventService.PublishWebSiteChanged(WebSite, AppChangeType.WebSiteCategory);
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

    private bool IsUrlIgnore(string url)
    {
        var ignoreUrlList = _appConfig.GetConfig().Behavior.IgnoreUrlList;
        return ignoreUrlList.Any(item => IsUrlMatch(url, item));
    }

    private bool IsUrlRegexIgnore(string url)
    {
        var ignoreUrlList = _appConfig.GetConfig().Behavior.IgnoreUrlList;
        return ignoreUrlList.Any(item => IsRegexPattern(item) && IsRegexMatch(url, item));
    }

    private static bool IsUrlMatch(string url, string pattern)
    {
        if (IsRegexPattern(pattern))
            return IsRegexMatch(url, pattern);
        return url.Contains(pattern);
    }

    private static bool IsRegexMatch(string input, string pattern)
    {
        try
        {
            return Regex.IsMatch(input, pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromSeconds(1));
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
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
        base.Dispose();
    }
}
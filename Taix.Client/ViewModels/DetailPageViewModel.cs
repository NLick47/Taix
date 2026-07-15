using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ReactiveUI;
using Taix.Client.Controls.Charts;
using Taix.Client.Controls.Charts.Model;
using Taix.Client.Controls.Select;
using Taix.Client.Events;
using Taix.Client.Librarys;
using Taix.Client.Logging;
using Taix.Client.Models;
using Taix.Client.Models.Navigation;
using Taix.Client.Servicers;
using Taix.Client.Servicers.Interfaces;
using Taix.Client.Shared.Helpers;
using Taix.Client.Shared.Librarys;
using Taix.Client.Shared.Models;
using Taix.Client.Shared.Models.Config;
using Taix.Client.Shared.Servicers.Interfaces;

namespace Taix.Client.ViewModels;

public class DetailPageViewModel : DetailPageModel
{
    private readonly IAppConfig _appConfig;
    private readonly IAppData _appDataService;
    private readonly ICategorys _categoryService;
    private readonly IClipboardService _clipboardService;
    private readonly IData _dataService;
    private readonly IDialogService _dialogService;
    private readonly IProcessService _processService;
    private readonly IToastService _toastService;
    private readonly INavigationDataService _navigationData;
    private readonly IContextMenuServicer _contextMenuService;
    private readonly IAppEventService _appEventService;
    private readonly IAppUpdateService _appUpdateService;

    public DetailPageViewModel(
        IData data,
        INavigationDataService navigationData,
        IAppConfig appConfig,
        ICategorys categories,
        IAppData appData,
        IDialogService dialogService,
        IClipboardService clipboardService,
        IProcessService processService,
        IToastService toastService,
        IContextMenuServicer contextMenuService,
        IAppEventService appEventService,
        IAppUpdateService appUpdateService)
    {
        _dataService = data;
        _navigationData = navigationData;
        _appConfig = appConfig;
        _categoryService = categories;
        _appDataService = appData;
        _dialogService = dialogService;
        _clipboardService = clipboardService;
        _processService = processService;
        _toastService = toastService;
        _contextMenuService = contextMenuService;
        _appEventService = appEventService;
        _appUpdateService = appUpdateService;

        BlockActionCommand = ReactiveCommand.CreateFromTask<object>(OnBlockActionAsync);
        ClearSelectMonthDataCommand = ReactiveCommand.CreateFromTask<object>(OnClearSelectMonthDataAsync);
        RefreshCommand = ReactiveCommand.CreateFromTask<object>(OnRefreshAsync);

        BlockActionCommand.DisposeWith(Disposables);
        ClearSelectMonthDataCommand.DisposeWith(Disposables);
        RefreshCommand.DisposeWith(Disposables);

        Initialize();
    }

    public ReactiveCommand<object, Unit> BlockActionCommand { get; }
    public ReactiveCommand<object, Unit> ClearSelectMonthDataCommand { get; }
    public ReactiveCommand<object, Unit> RefreshCommand { get; }

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

        WhenPropertyChanged(this, x => x.Date, _ => LoadDataAsync());
        WhenPropertyChanged(this, x => x.Category, _ => OnCategoryChangedAsync());
        WhenPropertyChanged(this, x => x.ChartDate, _ => LoadDayDataAsync());
        WhenPropertyChanged(this, x => x.TabbarSelectedIndex, _ => LoadChartDataAsync());
        WhenPropertyChanged(this, x => x.SelectedPeriod, p =>
        {
            if (p != null) TabbarSelectedIndex = p.Id;
            return Task.CompletedTask;
        });
        WhenPropertyChanged(this, x => x.WeekDate, _ => LoadWeekDataAsync());
        WhenPropertyChanged(this, x => x.MonthDate, _ => LoadMonthlyDataAsync());
        WhenPropertyChanged(this, x => x.YearDate, _ => LoadYearDataAsync());
    }

    public override async Task OnNavigatedToAsync()
    {
        AppModel? app = null;
        int periodType = 0;
        DateTime date = DateTime.Now;

        if (_navigationData.Data is DetailNavigationContext context)
        {
            app = context.App;
            periodType = context.PeriodType;
            date = context.Date;
        }
        else if (_navigationData.Data is AppModel appModel)
        {
            app = appModel;
        }

        if (app == null)
        {
            _toastService.Error(ResourceStrings.InvalidParameter);
            return;
        }

        IsRestoringState = true;
        App = app;

        _appEventService.AppChanged
            .Where(e => e.App.ID == App?.ID)
            .Subscribe(e => OnAppChanged(e.App, e.ChangeType))
            .DisposeWith(Disposables);

        Date = DateTime.Now;
        ChartDate = date;
        WeekDate = date;
        MonthDate = date;
        YearDate = date;
        TabbarSelectedIndex = periodType;
        SelectedPeriod = PeriodOptions[periodType];
        IsRestoringState = false;

        // 设置 ContextMenu 的 Tag 为当前 App 数据
        await UpdateContextMenuDataAsync();

        await ExecuteAsync(LoadDataCoreAsync);
        await LoadChartDataAsync();
        await ExecuteAsync(LoadInfoAsync);
    }

    /// <summary>
    /// 更新 ContextMenu 的数据上下文
    /// </summary>
    private async Task UpdateContextMenuDataAsync()
    {
        if (App == null) return;

        var chartData = new ChartsDataModel
        {
            Name = App.Description,
            Icon = App.IconFile,
            Data = new DailyLogModel { AppModel = App }
        };
        AppContextMenu = await _contextMenuService.CreateContextMenuAsync(ContextMenuType.AppDetail, chartData);
    }

    private Task LoadDataAsync() => ExecuteAsync(LoadDataCoreAsync);

    private async Task LoadDataCoreAsync(CancellationToken cancellationToken)
    {
        if (App == null) return;

        var monthData = await _dataService.GetProcessMonthLogListAsync(App.ID, Date, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var monthTotal = monthData.Sum(m => m.Time);
        Total = Time.ToString(monthTotal);

        var start = new DateTime(Date.Year, Date.Month, 1);
        var end = new DateTime(Date.Year, Date.Month, DateTime.DaysInMonth(Date.Year, Date.Month));
        var monthAllData = await _dataService.GetDateRangelogListAsync(start, end, cancellationToken: cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        LongDay = ResourceStrings.NoData;
        if (monthData.Count > 0)
        {
            var longDayData = monthData.OrderByDescending(m => m.Time).First();
            LongDay = string.Format(ResourceStrings.LongDayTips, longDayData.Date.ToString("dd"), Time.ToString(longDayData.Time));
        }

        var monthAllTotal = monthAllData.Sum(m => m.Time);
        Ratio = monthAllTotal > 0 ? (monthTotal / (double)monthAllTotal).ToString("P") : ResourceStrings.NoData;

        var chartData = ChartDataMapper.MapFromDailyLogs(monthData, App, orderByValue: false);
        if (chartData.Count == 0)
            chartData.Add(new ChartsDataModel { DateTime = Date });
        Data = chartData;
    }

    private async Task LoadInfoAsync(CancellationToken cancellationToken)
    {
        if (App == null) return;

        IsIgnore = IsProcessIgnore(App.Name, App.File);
        IsRegexIgnore = IsProcessRegexIgnore(App.Name, App.File);
        await LoadCategorys(App.CategoryID);
    }

    private Task LoadChartDataAsync() => ExecuteAsync(async ct =>
    {
        switch (TabbarSelectedIndex)
        {
            case 0:
                NameIndexStart = 0;
                await LoadDayDataAsync(ct);
                break;
            case 1:
                NameIndexStart = 0;
                await LoadWeekDataAsync(ct);
                break;
            case 2:
                NameIndexStart = 1;
                await LoadMonthlyDataAsync(ct);
                break;
            case 3:
                NameIndexStart = 1;
                await LoadYearDataAsync(ct);
                break;
        }
    });

    private Task OnRefreshAsync(object _) => LoadChartDataAsync();

    public override Task RefreshAsync() => LoadChartDataAsync();

    private async Task LoadCategorys(int categoryId)
    {
        var list = new List<SelectItemModel>();
        foreach (var item in await _categoryService.GetCategoriesAsync())
        {
            var option = new SelectItemModel
            {
                Id = item.ID,
                Data = item,
                Img = item.IconFile,
                Name = item.Name
            };
            list.Add(option);
            if (categoryId == option.Id)
                Category = option;
        }
        Categorys = list;
    }

    private async Task OnCategoryChangedAsync()
    {
        if (App == null || Category?.Data is not CategoryModel cat) return;
        if (App.CategoryID == cat.ID) return;

        await _appUpdateService.UpdateCategoryAsync(App.ID, cat.ID);
    }

    private void OnAppChanged(AppModel updatedApp, AppChangeType changeType)
    {
        App = updatedApp;

        if (changeType.HasFlag(AppChangeType.Category))
        {
            _ = ReloadCategoryAndRefreshAsync(updatedApp.CategoryID);
        }
        else
        {
            _ = UpdateContextMenuDataAsync();
        }
    }

    private async Task ReloadCategoryAndRefreshAsync(int categoryId)
    {
        await LoadCategorys(categoryId);
        await UpdateContextMenuDataAsync();
        await LoadDataAsync();
        await LoadChartDataAsync();
    }

    private async Task OnClearSelectMonthDataAsync(object _)
    {
        var isConfirm = await _dialogService.ShowConfirmDialogAsync(
            ResourceStrings.ClearConfirmation,
            string.Format(ResourceStrings.WantClearData, Date.Year, Date.Month));
        if (isConfirm) await ClearAsync();
    }

    private async Task ClearAsync()
    {
        if (App == null) return;
        _toastService.Toast(ResourceStrings.Processing);
        await _dataService.ClearAsync(App.ID, Date);
        await LoadDataAsync();
        await ExecuteAsync(LoadInfoAsync, trackLoading: false);
        _toastService.Toast(ResourceStrings.Cleared);
    }

    private async Task OnBlockActionAsync(object obj)
    {
        if (App == null) return;
        if (obj?.ToString() == "block")
        {
            var config = _appConfig.GetConfig();
            if (!config.Behavior.IgnoreProcessList.Contains(App.Name))
                config.Behavior.IgnoreProcessList.Add(App.Name);
            IsIgnore = true;
            _toastService.Success(string.Format(ResourceStrings.ApplicationNowIgnored, App.Name));
        }
        else
        {
            _appConfig.GetConfig().Behavior.IgnoreProcessList.Remove(App.Name);
            IsIgnore = false;
            _toastService.Success(string.Format(ResourceStrings.IgnoringApplicationCancelled, App.Name));
        }
        await _appConfig.SaveAsync();
    }

    private static readonly char[] RegexMetaChars = ['^', '$', '[', ']', '(', ')', '{', '}', '|', '+', '\\'];

    private bool IsProcessIgnore(string name, string? file)
    {
        var ignoreList = _appConfig.GetConfig().Behavior.IgnoreProcessList;
        return ignoreList.Contains(name);
    }

    private bool IsProcessRegexIgnore(string name, string? file)
    {
        var ignoreList = _appConfig.GetConfig().Behavior.IgnoreProcessList;
        return ignoreList.Any(item => IsWildcardOrRegexPattern(item) && IsProcessMatch(name, file, item));
    }

    private static bool IsWildcardOrRegexPattern(string pattern)
    {
        return pattern.Contains('*') || pattern.Contains('?') || pattern.IndexOfAny(RegexMetaChars) >= 0;
    }

    private static bool IsProcessMatch(string name, string? file, string pattern)
    {
        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(pattern))
            return false;
        return WildcardHelper.IsMatch(name, pattern) ||
               (!string.IsNullOrEmpty(file) && WildcardHelper.IsMatch(file, pattern));
    }

    #region 柱状图图表数据加载

    private async Task LoadDayDataAsync(CancellationToken cancellationToken = default)
    {
        if (TabbarSelectedIndex != 0) return;
        DataMaximum = 3600;
        if (App == null) return;
        var list = await _dataService.GetAppDayDataAsync(App.ID, ChartDate, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        ChartData = list.Select(item => new ChartsDataModel
        {
            Name = App.Description,
            Icon = App.IconFile,
            Values = item.Values
        }).ToList();
    }

    private async Task LoadWeekDataAsync(CancellationToken cancellationToken = default)
    {
        if (TabbarSelectedIndex != 1) return;
        DataMaximum = 0;
        if (App == null) return;
        var weekDateArr = GetWeekRange(WeekDate);

        var list = await _dataService.GetAppRangeDataAsync(App.ID, weekDateArr.Start, weekDateArr.End, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        string[] weekNames =
        [
            ResourceStrings.Monday, ResourceStrings.Tuesday, ResourceStrings.Wednesday, ResourceStrings.Thursday,
            ResourceStrings.Friday, ResourceStrings.Saturday, ResourceStrings.Sunday
        ];
        ChartData = list.Select(item => new ChartsDataModel
        {
            Name = App.Description,
            Icon = App.IconFile,
            Values = item.Values,
            ColumnNames = weekNames
        }).ToList();
    }

    private async Task LoadMonthlyDataAsync(CancellationToken cancellationToken = default)
    {
        if (TabbarSelectedIndex != 2) return;
        DataMaximum = 0;
        if (App == null) return;
        var dateArr = Time.GetMonthDate(MonthDate);
        var list = await _dataService.GetAppRangeDataAsync(App.ID, dateArr[0], dateArr[1], cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        ChartData = list.Select(item => new ChartsDataModel
        {
            Name = App.Description,
            Icon = App.IconFile,
            Values = item.Values
        }).ToList();
    }

    private async Task LoadYearDataAsync(CancellationToken cancellationToken = default)
    {
        if (TabbarSelectedIndex != 3) return;
        DataMaximum = 0;
        if (App == null) return;
        var list = await _dataService.GetAppYearDataAsync(App.ID, YearDate, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var names = new string[12];
        for (var i = 0; i < 12; i++)
            names[i] = Application.Current?.Resources[$"{i + 1}Month"] as string ?? $"{i + 1}";
        ChartData = list.Select(item => new ChartsDataModel
        {
            Name = App.Description,
            Icon = App.IconFile,
            Values = item.Values,
            ColumnNames = names
        }).ToList();
    }

    #endregion

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
        base.Dispose();
    }
}

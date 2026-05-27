using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using ReactiveUI;
using Taix.Client.Controls.Charts.Model;
using Taix.Client.Controls.Select;
using Taix.Client.Librarys;
using Taix.Client.Models;
using Taix.Client.Servicers;
using Taix.Client.Servicers.Interfaces;
using Taix.Client.Shared.Librarys;
using Taix.Client.Shared.Models;
using Taix.Client.Shared.Models.Db;
using Taix.Client.Shared.Servicers.Interfaces;
using Taix.Client.Views;

namespace Taix.Client.ViewModels;

public class IndexPageViewModel : IndexPageModel
{
    private readonly IData _dataService;
    private readonly IWebData _webDataService;
    private readonly IWebSiteContextMenuServicer _webSiteContextMenuService;
    private readonly IAppContextMenuServicer _appContextMenuService;
    private readonly INavigationService _navigationService;
    private readonly IAppConfig _appConfig;

    public IndexPageViewModel(
        IData data,
        IWebData webData,
        IWebSiteContextMenuServicer webSiteContextMenu,
        IAppContextMenuServicer appContextMenu,
        INavigationService navigationService,
        IAppConfig appConfig)
    {
        _dataService = data;
        _webDataService = webData;
        _webSiteContextMenuService = webSiteContextMenu;
        _appContextMenuService = appContextMenu;
        _navigationService = navigationService;
        _appConfig = appConfig;

        ToDetailCommand = ReactiveCommand.Create<object>(OnToDetail);
        RefreshCommand = ReactiveCommand.CreateFromTask<object>(OnRefreshAsync);

        InitializeStaticData();
    }

    public ReactiveCommand<object, Unit> ToDetailCommand { get; }
    public ReactiveCommand<object, Unit> RefreshCommand { get; }

    public List<SelectItemModel> MoreTypeOptions { get; private set; } = [];


    private void InitializeStaticData()
    {
        var config = _appConfig.GetConfig();
        FrequentUseNum = config.General.IndexPageFrequentUseNum + 1;
        MoreNum = config.General.IndexPageMoreNum + 1;

        PeriodOptions =
        [
            new SelectItemModel
            {
                Id = 0,
                Name = Application.Current?.FindResource("Today") as string ?? "Today"
            },
            new SelectItemModel
            {
                Id = 1,
                Name = Application.Current?.FindResource("ThisWeek") as string ?? "This Week"
            }
        ];
        SelectedPeriod = PeriodOptions[0];

        WhenPropertyChanged(this, x => x.SelectedPeriod, _ => LoadDataAsync());

        AppContextMenu = _appContextMenuService.GetContextMenu();
        WebSiteContextMenu = _webSiteContextMenuService.GetContextMenu();

        MoreTypeOptions =
        [
            new SelectItemModel
            {
                Id = 0,
                Name = Application.Current?.FindResource("App") as string ?? "App"
            },
            new SelectItemModel
            {
                Id = 1,
                Name = Application.Current?.FindResource("Website") as string ?? "Website"
            }
        ];
        MoreType = MoreTypeOptions[0];
    }

    /// <summary>
    /// 页面导航到达时执行数据加载。
    /// </summary>
    public override Task OnNavigatedToAsync()
    {
        _ = LoadDataAsync();
        return Task.CompletedTask;
    }

    private Task OnRefreshAsync(object _) => LoadDataAsync();

    private Task LoadDataAsync()
    {
        CancelAndResetLoadToken();

        if (TabbarSelectedIndex == 0)
            return ExecuteAsync(LoadTodayDataAsync);

        if (TabbarSelectedIndex == 1)
            return ExecuteAsync(LoadThisWeekDataAsync);

        return Task.CompletedTask;
    }

    /// <summary>
    /// 加载本周数据
    /// </summary>
    private async Task LoadThisWeekDataAsync(CancellationToken cancellationToken)
    {
        var week = Time.GetThisWeekDate();

        var appDataTask = _dataService.GetThisWeeklogListAsync(cancellationToken);
        var webDataTask = _webDataService.GetDateRangeWebSiteListAsync(week[0], week[1], cancellationToken: cancellationToken);

        await Task.WhenAll(appDataTask, webDataTask);
        cancellationToken.ThrowIfCancellationRequested();

        var appList = await appDataTask;
        var appChartData = ChartDataMapper.MapFromDailyLogs(appList);
        WeekData = appChartData;
        AppFrequentUseData = appChartData.Take(FrequentUseNum).ToList();
        AppMoreData = appChartData.Skip(FrequentUseNum).Take(MoreNum).ToList();

        var webList = await webDataTask;
        var webChartData = ChartDataMapper.MapFromWebSites(webList);
        WebFrequentUseData = webChartData.Take(FrequentUseNum).ToList();
        WebMoreData = webChartData.Skip(FrequentUseNum).Take(MoreNum).ToList();
    }

    /// <summary>
    /// 加载今日数据
    /// </summary>
    private async Task LoadTodayDataAsync(CancellationToken cancellationToken)
    {
        var today = DateTime.Now.Date;

        var appDataTask = _dataService.GetDateRangelogListAsync(today, today, cancellationToken: cancellationToken);
        var webDataTask = _webDataService.GetDateRangeWebSiteListAsync(today, today, cancellationToken: cancellationToken);

        await Task.WhenAll(appDataTask, webDataTask);
        cancellationToken.ThrowIfCancellationRequested();

        var appList = await appDataTask;
        var appChartData = ChartDataMapper.MapFromDailyLogs(appList);
        WeekData = appChartData;
        AppFrequentUseData = appChartData.Take(FrequentUseNum).ToList();
        AppMoreData = appChartData.Skip(FrequentUseNum).Take(MoreNum).ToList();

        var webList = await webDataTask;
        var webChartData = ChartDataMapper.MapFromWebSites(webList);
        WebFrequentUseData = webChartData.Take(FrequentUseNum).ToList();
        WebMoreData = webChartData.Skip(FrequentUseNum).Take(MoreNum).ToList();
    }

    private void OnToDetail(object obj)
    {
        if (obj is not ChartsDataModel chartData) return;

        if (chartData.Data is DailyLogModel { AppModel: not null } dailyModel)
        {
            _navigationService.NavigateTo(nameof(DetailPage), dailyModel.AppModel);
        }
        else if (chartData.Data is WebSiteModel webSiteModel)
        {
            _navigationService.NavigateTo(nameof(WebSiteDetailPage), webSiteModel);
        }
    }

    public override void Dispose()
    {
        (ToDetailCommand as IDisposable).Dispose();
        (RefreshCommand as IDisposable).Dispose();
        WeekData = [];
        AppFrequentUseData = [];
        AppMoreData = [];
        WebFrequentUseData = [];
        WebMoreData = [];
        base.Dispose();
    }
}

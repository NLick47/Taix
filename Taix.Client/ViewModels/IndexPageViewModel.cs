using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Taix.Client.Controls.Charts.Model;
using Taix.Client.Controls.Select;
using Taix.Client.Events;
using Taix.Client.Foundation;
using Taix.Client.Foundation.Rx;
using Taix.Client.Librarys;
using Taix.Client.Models;
using Taix.Client.Models.Navigation;
using Taix.Client.Servicers;
using Taix.Client.Servicers.Interfaces;
using Taix.Client.Shared.Librarys;
using Taix.Client.Shared.Models;
using Taix.Client.Shared.Models.Web;
using Taix.Client.Shared.Servicers.Interfaces;
using Taix.Client.Views;

namespace Taix.Client.ViewModels;

public partial class IndexPageViewModel : IndexPageModel
{
    private readonly IData _dataService;
    private readonly IWebData _webDataService;
    private readonly INavigationService _navigationService;
    private readonly IAppConfig _appConfig;
    private readonly IStateService _stateService;
    private readonly IAppEventService _appEventService;

    public IndexPageViewModel(
        IData data,
        IWebData webData,
        INavigationService navigationService,
        IAppConfig appConfig,
        IStateService stateService,
        IAppEventService appEventService)
    {
        _dataService = data;
        _webDataService = webData;
        _navigationService = navigationService;
        _appConfig = appConfig;
        _stateService = stateService;
        _appEventService = appEventService;

        ToDetailCommand = AsyncRelayCommand.Create<object>(OnToDetail).DisposeWith(Disposables);
        RefreshCommand = AsyncRelayCommand.CreateFromTask<object>(OnRefreshAsync).DisposeWith(Disposables);

        InitializeStaticData();

        RefreshOnChange(
            _appEventService.AppChanged,
            _appEventService.WebSiteChanged,
            LoadDataAsync);
    }

    public ICommand ToDetailCommand { get; }
    public ICommand RefreshCommand { get; }


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

        WhenPropertyChanged(this, x => x.TabbarSelectedIndex, _ => LoadDataAsync());

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

    public override Task OnNavigatedToAsync()
    {
        var restored = TryRestoreState(_navigationService, _stateService);
        return RestoreCachedDataOrLoadAsync(
            restored, _stateService, _appEventService, LoadDataAsync);
    }

    public override void OnNavigatedFrom()
    {
        SaveState(_stateService);
        SaveCachedDataVersion(_stateService, _appEventService);
        base.OnNavigatedFrom();
    }

    private Task OnRefreshAsync(object _) => LoadDataAsync();

    public override Task RefreshAsync() => LoadDataAsync();

    private Task LoadDataAsync()
    {
        return ExecuteAsync(async ct =>
        {
            if (TabbarSelectedIndex == 0)
            {
                await LoadTodayDataAsync(ct);
            }
            else if (TabbarSelectedIndex == 1)
            {
                await LoadThisWeekDataAsync(ct);
            }
        });
    }

    /// <summary>
    /// 加载本周数据
    /// </summary>
    private async Task LoadThisWeekDataAsync(CancellationToken cancellationToken)
    {
        var week = Time.GetThisWeekDate();

        var appDataTask = _dataService.GetDateRangelogListAsync(week[0], week[1], cancellationToken: cancellationToken);
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
        var date = DateTime.Now.Date;

        if (chartData.Data is DailyLogModel { AppModel: not null } dailyModel)
            _navigationService.NavigateTo(nameof(DetailPage), DetailNavigationContext.Create(dailyModel.AppModel, TabbarSelectedIndex, date));
        else if (chartData.Data is WebSiteModel webSiteModel)
            _navigationService.NavigateTo(nameof(WebSiteDetailPage), WebSiteDetailNavigationContext.Create(webSiteModel, TabbarSelectedIndex, date));
    }

    public override void Dispose()
    {
        WeekData = [];
        AppFrequentUseData = [];
        AppMoreData = [];
        WebFrequentUseData = [];
        WebMoreData = [];
        base.Dispose();
    }

}

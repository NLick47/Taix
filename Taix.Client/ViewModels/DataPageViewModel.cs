using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Input;
using ReactiveUI;
using Taix.Client.Controls.Charts.Model;
using Taix.Client.Librarys;
using Taix.Client.Models;
using Taix.Client.Servicers;
using Taix.Client.Servicers.Interfaces;
using Taix.Client.Shared.Models;
using Taix.Client.Shared.Models.Db;
using Taix.Client.Shared.Servicers.Interfaces;
using Taix.Client.Views;

namespace Taix.Client.ViewModels;

public class DataPageViewModel : DataPageModel
{
    private readonly IWebData _webDataService;
    private readonly IWebSiteContextMenuServicer _webSiteContextMenuService;
    private readonly IAppConfig _appConfig;
    private readonly IAppContextMenuServicer _appContextMenuService;
    private readonly IData _dataService;
    private readonly INavigationService _navigationService;

    public DataPageViewModel(
        IData data,
        IAppConfig appConfig,
        IWebSiteContextMenuServicer webSiteContextMenu,
        IAppContextMenuServicer contextMenu,
        IWebData webData,
        INavigationService navigationService)
    {
        _dataService = data;
        _appConfig = appConfig;
        _appContextMenuService = contextMenu;
        _webSiteContextMenuService = webSiteContextMenu;
        _webDataService = webData;
        _navigationService = navigationService;

        ToDetailCommand = ReactiveCommand.Create<object>(OnToDetail);
        Initialize();
    }

    public ICommand ToDetailCommand { get; }

    public override void Dispose()
    {
        (ToDetailCommand as IDisposable)?.Dispose();
        base.Dispose();
    }

    private void Initialize()
    {
        TabbarData = [ResourceStrings.Daily, ResourceStrings.Monthly, ResourceStrings.Yearly];
        AppContextMenu = _appContextMenuService.GetContextMenu();

        DayDate = DateTime.Now.Date;
        MonthDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
        YearDate = new DateTime(DateTime.Now.Year, 1, 1);

        WhenPropertyChanged(this, x => x.DayDate, date => OnDateChangedAsync(date, 0));
        WhenPropertyChanged(this, x => x.MonthDate, date => OnDateChangedAsync(date, 1));
        WhenPropertyChanged(this, x => x.YearDate, date => OnDateChangedAsync(date, 2));

        WhenPropertyChanged(this, x => x.TabbarSelectedIndex, async index =>
        {
            if (index == 0 && DayDate == DateTime.MinValue)
                DayDate = DateTime.Now.Date;
            else if (index == 1 && MonthDate == DateTime.MinValue)
                MonthDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            else if (index == 2 && YearDate == DateTime.MinValue)
                YearDate = new DateTime(DateTime.Now.Year, 1, 1);
            await Task.CompletedTask;
        }, skipInitial: false);

        WhenPropertyChanged(this, x => x.ShowType, async _ =>
        {
            await LoadDataAsync(DayDate, 0);
            await LoadDataAsync(MonthDate, 1);
            await LoadDataAsync(YearDate, 2);
            AppContextMenu = ShowType.Id == 0
                ? _appContextMenuService.GetContextMenu()
                : _webSiteContextMenuService.GetContextMenu();
        });

        TabbarSelectedIndex = 0;
    }

    public override Task OnNavigatedToAsync()
    {
        _ = LoadDataAsync(DayDate, 0);
        _ = LoadDataAsync(MonthDate, 1);
        _ = LoadDataAsync(YearDate, 2);
        return Task.CompletedTask;
    }

    private Task OnDateChangedAsync(DateTime date, int dataType)
    {
        return LoadDataAsync(date, dataType);
    }

    private void OnToDetail(object obj)
    {
        if (obj is not ChartsDataModel chartData) return;

        if (chartData.Data is DailyLogModel { AppModel: not null } model)
        {
            _navigationService.NavigateTo(nameof(DetailPage), model.AppModel);
        }
        else if (chartData.Data is WebSiteModel webSite)
        {
            _navigationService.NavigateTo(nameof(WebSiteDetailPage), webSite);
        }
    }

    private Task LoadDataAsync(DateTime date, int dataType)
    {
        return ExecuteAsync(async ct =>
        {
            var (start, end) = GetDateRange(date, dataType);
            var ignoreList = _appConfig.GetConfig().Behavior.IgnoreProcessList;
            var ignoreUrlList = _appConfig.GetConfig().Behavior.IgnoreUrlList;

            List<ChartsDataModel> chartData;
            if (ShowType.Id == 0)
            {
                var result = await _dataService.GetDateRangelogListAsync(start, end, cancellationToken: ct);
                ct.ThrowIfCancellationRequested();
                chartData = ChartDataMapper.MapFromDailyLogs(result, includeBadges: true, ignoreList);
            }
            else
            {
                var result = await _webDataService.GetWebSiteLogListAsync(start, end, ct);
                ct.ThrowIfCancellationRequested();
                chartData = ChartDataMapper.MapFromWebSites(result, includeBadges: true, ignoreUrlList);
            }

            ct.ThrowIfCancellationRequested();

            switch (dataType)
            {
                case 0: Data = chartData; break;
                case 1: MonthData = chartData; break;
                case 2: YearData = chartData; break;
            }
        });
    }

    private static (DateTime Start, DateTime End) GetDateRange(DateTime date, int dataType)
    {
        return dataType switch
        {
            0 => (date.Date, date.Date.AddDays(1).AddTicks(-1)),
            1 => (new DateTime(date.Year, date.Month, 1), new DateTime(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month), 23, 59, 59)),
            2 => (new DateTime(date.Year, 1, 1), new DateTime(date.Year, 12, 31, 23, 59, 59)),
            _ => (date, date)
        };
    }
}

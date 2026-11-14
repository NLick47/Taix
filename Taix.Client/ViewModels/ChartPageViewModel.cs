using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
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
using Taix.Client.Shared.Models.Config;
using Taix.Client.Shared.Models.Data;
using Taix.Client.Shared.Models.Web;
using Taix.Client.Shared.Servicers.Interfaces;
using Taix.Client.Views;

namespace Taix.Client.ViewModels;

public partial class ChartPageViewModel : ChartPageModel
{
    private readonly ConfigModel _config;
    private readonly IData _dataService;
    private readonly IWebData _webDataService;
    private readonly INavigationService _navigationService;
    private readonly IStateService _stateService;
    private readonly IAppEventService _appEventService;
    private int _appCount;
    private double _totalTime;

    public ChartPageViewModel(
        IData data,
        INavigationService navigationService,
        IWebData webData,
        IAppConfig appConfig,
        IStateService stateService,
        IAppEventService appEventService)
    {
        _dataService = data;
        _navigationService = navigationService;
        _webDataService = webData;
        _stateService = stateService;
        _config = appConfig.GetConfig();
        _appEventService = appEventService;

        ToDetailCommand = AsyncRelayCommand.Create<object>(OnToDetail).DisposeWith(Disposables);
        RefreshCommand = AsyncRelayCommand.CreateFromTask<object>(OnRefreshAsync).DisposeWith(Disposables);

        Initialize();

        RefreshOnChange(
            _appEventService.AppChanged,
            _appEventService.WebSiteChanged,
            RefreshAsync);
    }

    public ICommand ToDetailCommand { get; }
    public ICommand RefreshCommand { get; }

    private void Initialize()
    {
        TabbarData = [ResourceStrings.Daily, ResourceStrings.Weekly, ResourceStrings.Monthly, ResourceStrings.Yearly];

        PeriodOptions =
        [
            new() { Id = 0, Name = ResourceStrings.Daily },
            new() { Id = 1, Name = ResourceStrings.Weekly },
            new() { Id = 2, Name = ResourceStrings.Monthly },
            new() { Id = 3, Name = ResourceStrings.Yearly }
        ];

        ShowType = ShowTypeOptions[0];
        SelectedPeriod = PeriodOptions[0];
        Date = DateTime.Now;
        WeekDate = DateTime.Now;
        MonthDate = DateTime.Now;
        YearDate = DateTime.Now;
        TabbarSelectedIndex = 0;

        WhenPropertyChanged(this, x => x.Date, _ => OnDateChangedAsync());
        WhenPropertyChanged(this, x => x.TabbarSelectedIndex, _ => OnTabbarChangedAsync());
        WhenPropertyChanged(this, x => x.WeekDate, _ => OnWeekDateChangedAsync());
        WhenPropertyChanged(this, x => x.MonthDate, _ => OnMonthDateChangedAsync());
        WhenPropertyChanged(this, x => x.YearDate, _ => OnYearDateChangedAsync());
        WhenPropertyChanged(this, x => x.ShowType, _ => OnShowTypeChangedAsync());
        ObservablePropertyChangedExtensions.WhenPropertyChanged(this, x => x.ColumnSelectedIndex)
            .ObserveOn(AvaloniaContextScheduler.Instance)
            .Select(_ => RxObservable.FromAsync(async ct => await LoadSelectedColDataAsync(ct)))
            .Switch()
            .Subscribe()
            .DisposeWith(Disposables);


        WhenPropertyChanged(this, x => x.SelectedPeriod, p =>
        {
            if (p != null) TabbarSelectedIndex = p.Id;
            return Task.CompletedTask;
        });
        ObservablePropertyChangedExtensions.WhenPropertyChanged(this, x => x.WebColSelectedIndex)
            .ObserveOn(AvaloniaContextScheduler.Instance)
            .Select(_ => RxObservable.FromAsync(async ct => await LoadWebSitesColSelectedDataAsync(ct)))
            .Switch()
            .Subscribe()
            .DisposeWith(Disposables);
    }

    public override Task OnNavigatedToAsync()
    {
        var restored = TryRestoreState(_navigationService, _stateService);
        return RestoreCachedDataOrLoadAsync(
            restored,
            _stateService,
            _appEventService,
            () => ExecuteAsync(LoadDataAsync));
    }

    public override void OnNavigatedFrom()
    {
        SaveState(_stateService);
        SaveCachedDataVersion(_stateService, _appEventService);
        base.OnNavigatedFrom();
    }

    private Task OnDateChangedAsync() => ExecuteAsync(LoadDayDataAsync);

    private Task OnTabbarChangedAsync() => ExecuteAsync(async ct =>
    {
        IsCanColumnSelect = true;
        await LoadDataAsync(ct);
    });

    private Task OnWeekDateChangedAsync() => ExecuteAsync(async ct =>
    {
        if (TabbarSelectedIndex != 1) return;
        await LoadWeekDataAsync(ct);
    });

    private Task OnMonthDateChangedAsync() => ExecuteAsync(async ct =>
    {
        if (TabbarSelectedIndex != 2) return;
        await LoadMonthlyDataAsync(ct);
    });

    private Task OnYearDateChangedAsync() => ExecuteAsync(async ct =>
    {
        if (TabbarSelectedIndex != 3) return;
        await LoadYearDataAsync(ct);
    });

    private Task OnShowTypeChangedAsync() => ExecuteAsync(LoadDataAsync);

    private Task OnRefreshAsync(object _) => ExecuteAsync(LoadDataAsync);

    public override Task RefreshAsync() => ExecuteAsync(LoadDataAsync);



    private void OnToDetail(object obj)
    {
        if (obj is not ChartsDataModel chartData) return;

        var date = TabbarSelectedIndex switch { 0 => Date, 1 => WeekDate, 2 => MonthDate, 3 => YearDate, _ => DateTime.Now };

        if (chartData.Data is WebSiteModel webSite)
            _navigationService.NavigateTo(nameof(WebSiteDetailPage), WebSiteDetailNavigationContext.Create(webSite, TabbarSelectedIndex, date));
        else if (chartData.Data is DailyLogModel { AppModel: not null } daily)
            _navigationService.NavigateTo(nameof(DetailPage), DetailNavigationContext.Create(daily.AppModel, TabbarSelectedIndex, date));
        else if (chartData.Data is HoursLogModel { AppModel: not null } hours)
            _navigationService.NavigateTo(nameof(DetailPage), DetailNavigationContext.Create(hours.AppModel, 0, Date));
    }

    private async Task LoadDataAsync(CancellationToken cancellationToken)
    {
        switch (TabbarSelectedIndex)
        {
            case 0:
                NameIndexStart = 0;
                await LoadDayDataAsync(cancellationToken);
                break;
            case 1:
                NameIndexStart = 0;
                await LoadWeekDataAsync(cancellationToken);
                break;
            case 2:
                NameIndexStart = 1;
                await LoadMonthlyDataAsync(cancellationToken);
                break;
            case 3:
                NameIndexStart = 1;
                await LoadYearDataAsync(cancellationToken);
                break;
        }


    }

    private async Task LoadDayDataAsync(CancellationToken cancellationToken)
    {
        if (ShowType.Id == 0)
        {
            var prev = Date.Date.AddDays(-1);
            var (chartData, categoryData, bundle) = await LoadChartBundleAsync(
                Date.Date, Date.Date, prev, prev, "hour", null, cancellationToken);

            var totalUse = chartData.Sum(m => m.Values.Sum());
            _totalTime = totalUse;
            Data = chartData;
            RadarData = categoryData;
            ColumnSelectedIndex = -1;
            WebColSelectedIndex = -1;
            TotalHours = Time.ToHoursString(totalUse);

            ApplyTopAndDiffFromBundle(bundle);
        }
        else
        {
            ColumnSelectedIndex = -1;
            WebColSelectedIndex = -1;
            await LoadWebDataAsync(Date, Date, cancellationToken);
        }
    }

    private async Task LoadWeekDataAsync(CancellationToken cancellationToken)
    {
        ColumnSelectedIndex = -1;
        WebColSelectedIndex = -1;
        DataMaximum = 0;
        var culture = SystemLanguage.CurrentCultureInfo;
        var weekDateArr = GetWeekRange(WeekDate);
        var toText = Application.Current?.Resources["To"] as string ?? "To";
        WeekDateStr = $"{weekDateArr.Start.ToString("d", culture)} {toText} {weekDateArr.End.ToString("d", culture)}";

        if (ShowType.Id == 0)
        {
            string[] weekNames = [ResourceStrings.Monday, ResourceStrings.Tuesday, ResourceStrings.Wednesday, ResourceStrings.Thursday, ResourceStrings.Friday, ResourceStrings.Saturday, ResourceStrings.Sunday];

            var prevWeek = GetWeekRange(WeekDate.AddDays(-7));
            var (chartData, categoryData, bundle) = await LoadChartBundleAsync(
                weekDateArr.Start, weekDateArr.End, prevWeek.Start, prevWeek.End, "day", weekNames, cancellationToken);

            var totalUse = chartData.Sum(m => m.Values.Sum());
            _totalTime = totalUse;
            Data = chartData;
            RadarData = categoryData;
            TotalHours = Time.ToHoursString(totalUse);

            ApplyTopAndDiffFromBundle(bundle);
        }
        else
        {
            await LoadWebDataAsync(weekDateArr.Start, weekDateArr.End, cancellationToken);
        }
    }

    private async Task LoadMonthlyDataAsync(CancellationToken cancellationToken)
    {
        ColumnSelectedIndex = -1;
        WebColSelectedIndex = -1;
        DataMaximum = 0;
        var dateArr = Time.GetMonthDate(MonthDate);

        if (ShowType.Id == 0)
        {
            var prevMonth = Time.GetMonthDate(MonthDate.AddMonths(-1));
            var (chartData, categoryData, bundle) = await LoadChartBundleAsync(
                dateArr[0], dateArr[1], prevMonth[0], prevMonth[1], "day", null, cancellationToken);

            var totalUse = chartData.Sum(m => m.Values.Sum());
            _totalTime = totalUse;
            Data = chartData;
            RadarData = categoryData;
            TotalHours = Time.ToHoursString(totalUse);

            ApplyTopAndDiffFromBundle(bundle);
        }
        else
        {
            await LoadWebDataAsync(dateArr[0], dateArr[1], cancellationToken);
        }
    }

    private async Task LoadYearDataAsync(CancellationToken cancellationToken)
    {
        ColumnSelectedIndex = -1;
        WebColSelectedIndex = -1;
        DataMaximum = 0;
        var names = new string[12];
        for (var i = 0; i < 12; i++)
            names[i] = Application.Current?.Resources[$"{i + 1}Month"] as string ?? $"{i + 1}";

        if (ShowType.Id == 0)
        {
            var yearArr = Time.GetYearDate(YearDate);
            var prevYear = Time.GetYearDate(YearDate.AddYears(-1));
            var (chartData, categoryData, bundle) = await LoadChartBundleAsync(
                yearArr[0], yearArr[1], prevYear[0], prevYear[1], "month", names, cancellationToken);

            var totalUse = chartData.Sum(m => m.Values.Sum());
            _totalTime = totalUse;
            Data = chartData;
            RadarData = categoryData;
            TotalHours = Time.ToHoursString(totalUse);

            ApplyTopAndDiffFromBundle(bundle);
        }
        else
        {
            var yearArr = Time.GetYearDate(YearDate);
            await LoadWebDataAsync(yearArr[0], yearArr[1], cancellationToken);
        }
    }

    private async Task<(List<ChartsDataModel> data, List<ChartsDataModel> categoryData, ChartBundleModel bundle)> LoadChartBundleAsync(
        DateTime start, DateTime end, DateTime? prevStart, DateTime? prevEnd, string bucket, string[]? columnNames,
        CancellationToken cancellationToken)
    {
        var bundle = await _dataService.GetChartBundleAsync(start, end, prevStart, prevEnd, bucket, 5, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        var categoryData = new List<ChartsDataModel>();
        foreach (var item in bundle.Columns)
        {
            categoryData.Add(new ChartsDataModel
            {
                Name = item.Name,
                Icon = item.IconFile,
                Values = item.Values,
                Color = item.Color,
                ColumnNames = columnNames
            });
        }

        categoryData = categoryData.OrderByDescending(c => c.Values.Sum()).ToList();

        // Data 与 RadarData 各持一份：两个图表共享同一实例时，一方的就地修改会污染另一方
        return (categoryData, categoryData.ToList(), bundle);
    }

    private void ApplyTopAndDiffFromBundle(ChartBundleModel bundle)
    {
        TopData = ChartDataMapper.MapFromDailyLogs(bundle.Top);
        TopHours = TopData.Count > 0 ? Time.ToHoursString(TopData[0].Value) : "0";

        _appCount = bundle.AppCount;
        AppCount = _appCount.ToString();
        Top1App = TopData.Count > 0 && TopData[0].Data is DailyLogModel { AppModel: not null } model ? model.AppModel : null;

        var lastTotalTime = bundle.TotalSecondsPrev;
        var diffTotalTime = lastTotalTime == 0
            ? (_totalTime > 0 ? 100 : 0)
            : (_totalTime - lastTotalTime) / lastTotalTime * 100;

        DiffTotalTimeType = diffTotalTime > 0 ? "1" : diffTotalTime < 0 ? "-1" : "0";
        DiffTotalTimeValue = DiffTotalTimeType == "0" ? string.Empty :
            diffTotalTime >= 100 ? "100%" : Math.Abs(diffTotalTime).ToString("f2") + "%";

        var diffAppCount = _appCount - bundle.AppCountPrev;
        DiffAppCountType = diffAppCount > 0 ? "1" : diffAppCount < 0 ? "-1" : "0";
        DiffAppCountValue = DiffAppCountType == "0" ? string.Empty : Math.Abs(diffAppCount).ToString();

        LastWebTotalTime = bundle.WebPrev.DurationTotal;
        LastWebSiteCount = bundle.WebPrev.SitesTotal;
        LastWebPageCount = bundle.WebPrev.PagesTotal;
    }

    private async Task LoadSelectedColDataAsync(CancellationToken cancellationToken = default)
    {
        var culture = SystemLanguage.CurrentCultureInfo;
        if (ColumnSelectedIndex < 0)
        {
            DayHoursSelectedTime = string.Empty;
            return;
        }

        List<ChartsDataModel> chartsDatas;
        if (TabbarSelectedIndex == 0)
        {
            var time = new DateTime(Date.Year, Date.Month, Date.Day, ColumnSelectedIndex, 0, 0);
            var format = $"{culture.DateTimeFormat.ShortDatePattern} HH";
            DayHoursSelectedTime = time.ToString(format, culture);
            var hoursModelList = await _dataService.GetTimeRangelogListAsync(time, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            chartsDatas = ChartDataMapper.MapFromHoursLogs(hoursModelList, true);
        }
        else
        {
            DateTime start, end;
            if (TabbarSelectedIndex == 1)
            {
                var weekDateArr = GetWeekRange(WeekDate);
                var time = weekDateArr.Start.AddDays(ColumnSelectedIndex);
                DayHoursSelectedTime = time.ToString("d", culture);
                start = end = time;
            }
            else if (TabbarSelectedIndex == 2)
            {
                var dateArr = Time.GetMonthDate(MonthDate);
                var time = dateArr[0].AddDays(ColumnSelectedIndex);
                DayHoursSelectedTime = time.ToString("d", culture);
                start = end = time;
            }
            else
            {
                start = new DateTime(YearDate.Year, ColumnSelectedIndex + 1, 1);
                end = new DateTime(start.Year, start.Month, DateTime.DaysInMonth(start.Year, start.Month), 23, 59, 59);
                DayHoursSelectedTime = start.ToString(culture.DateTimeFormat.YearMonthPattern, culture);
            }
            var daysModelList = await _dataService.GetDateRangelogListAsync(start, end, cancellationToken: cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            chartsDatas = ChartDataMapper.MapFromDailyLogs(daysModelList, includeBadges: true);
        }

        cancellationToken.ThrowIfCancellationRequested();
        DayHoursData = chartsDatas;
    }

    #region 网页数据

    private async Task LoadWebDataAsync(DateTime start, DateTime end, CancellationToken cancellationToken)
    {
        var bundle = await _webDataService.GetWebChartBundleAsync(start, end, 10, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        ApplyCategoriesStatisticsFromBundle(bundle.CategoryStatistics, bundle.Categories);
        WebSitesTopData = ChartDataMapper.MapFromWebSites(bundle.TopSites);
        ApplyWebBrowseStatisticsFromBundle(bundle.CategoryColumns, bundle.Categories);

        WebTotalTime = bundle.DurationTotal;
        WebSiteCount = bundle.SitesTotal;
        WebPageCount = bundle.PagesTotal;
        WebTotalTimeText = Time.ToHoursString(WebTotalTime);
    }

    private void ApplyCategoriesStatisticsFromBundle(List<InfrastructureDataModel> data, IReadOnlyList<WebSiteCategoryModel> categories)
    {
        var chartsDatas = new List<ChartsDataModel>();
        var categoryDict = categories.ToDictionary(c => c.ID);
        foreach (var item in data)
        {
            if (!categoryDict.TryGetValue(item.ID, out var category))
            {
                category = new WebSiteCategoryModel { Name = "Unknown", Color = "#ccc", IconFile = "" };
            }
#pragma warning disable CS8601
            chartsDatas.Add(new ChartsDataModel
            {
                Name = item.Name,
                Value = item.Value,
                Data = item,
                Color = category.Color,
                PopupText = item.Name + " " + Time.ToString((int)item.Value),
                Icon = category.IconFile
            });
#pragma warning restore CS8601
        }
        WebCategoriesPieData = chartsDatas.OrderByDescending(m => m.Value).ToList();
    }

    private void ApplyWebBrowseStatisticsFromBundle(List<ColumnDataModel> data, IReadOnlyList<WebSiteCategoryModel> categories)
    {
        var chartData = new List<ChartsDataModel>();
        var categoryDict = categories.ToDictionary(c => c.ID);
        var emptyCategory = new WebSiteCategoryModel
        {
            ID = 0,
            Name = "Unknown",
            IconFile = "",
            Color = "#999999"
        };

        string[]? colNames = null;
        if (TabbarSelectedIndex == 1)
        {
            colNames = [ResourceStrings.Monday, ResourceStrings.Tuesday, ResourceStrings.Wednesday, ResourceStrings.Thursday, ResourceStrings.Friday, ResourceStrings.Saturday, ResourceStrings.Sunday];
        }
        else if (TabbarSelectedIndex == 3)
        {
            colNames = new string[12];
            for (var i = 0; i < 12; i++) colNames[i] = i + 1 + ResourceStrings.Month;
        }

        foreach (var item in data)
        {
            if (!categoryDict.TryGetValue(item.CategoryID, out var category))
                category = emptyCategory;
            if (category != null)
            {
#pragma warning disable CS8601
                chartData.Add(new ChartsDataModel
                {
                    Name = category.Name,
                    Icon = category.IconFile,
                    Values = item.Values,
                    Color = category.Color,
                    ColumnNames = colNames
                });
#pragma warning restore CS8601
            }
        }
        WebBrowseStatisticsData = chartData.OrderByDescending(m => m.Values.Sum()).ToList();
    }

    private async Task LoadWebSitesColSelectedDataAsync(CancellationToken cancellationToken = default)
    {
        if (WebColSelectedIndex < 0)
        {
            WebSitesColSelectedTimeText = string.Empty;
            WebSitesColSelectedData = [];
            return;
        }

        var culture = SystemLanguage.CurrentCultureInfo;
        DateTime startTime, endTime;
        bool isTime = false;

        if (TabbarSelectedIndex == 0)
        {
            var format = $"{culture.DateTimeFormat.ShortDatePattern} HH";
            var time = new DateTime(Date.Year, Date.Month, Date.Day, WebColSelectedIndex, 0, 0);
            WebSitesColSelectedTimeText = time.ToString(format, culture);
            isTime = true;
            startTime = endTime = time;
        }
        else if (TabbarSelectedIndex == 1)
        {
            var weekDateArr = GetWeekRange(WeekDate);
            var time = weekDateArr.Start.AddDays(WebColSelectedIndex);
            WebSitesColSelectedTimeText = time.ToString("d", culture);
            startTime = endTime = time;
        }
        else if (TabbarSelectedIndex == 2)
        {
            var dateArr = Time.GetMonthDate(MonthDate);
            var time = dateArr[0].AddDays(WebColSelectedIndex);
            WebSitesColSelectedTimeText = time.ToString("d", culture);
            startTime = endTime = time;
        }
        else
        {
            startTime = new DateTime(YearDate.Year, WebColSelectedIndex + 1, 1);
            endTime = new DateTime(startTime.Year, startTime.Month, DateTime.DaysInMonth(startTime.Year, startTime.Month), 23, 59, 59);
            WebSitesColSelectedTimeText = startTime.ToString(culture.DateTimeFormat.YearMonthPattern);
        }

        var chartData = ChartDataMapper.MapFromWebSites(await _webDataService.GetDateRangeWebSiteListAsync(startTime, endTime, 0, -1, isTime, cancellationToken), includeBadges: true);
        cancellationToken.ThrowIfCancellationRequested();
        WebSitesColSelectedData = chartData;
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
        Data = [];
        TopData = [];
        base.Dispose();
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using ReactiveUI;
using ReactiveUI.Avalonia;
using Taix.Client.Controls.Charts.Model;
using Taix.Client.Controls.Select;
using Taix.Client.Librarys;
using Taix.Client.Models;
using Taix.Client.Servicers;
using Taix.Client.Servicers.Interfaces;
using Taix.Client.Shared.Librarys;
using Taix.Client.Shared.Models;
using Taix.Client.Shared.Models.Config;
using Taix.Client.Shared.Models.Data;
using Taix.Client.Shared.Models.Db;
using Taix.Client.Shared.Servicers.Interfaces;
using Taix.Client.Views;

namespace Taix.Client.ViewModels;

public class ChartPageViewModel : ChartPageModel
{
    private readonly ConfigModel _config;
    private readonly IData _dataService;
    private readonly IWebData _webDataService;
    private readonly IWebSiteContextMenuServicer _webSiteContextMenuService;
    private readonly IAppContextMenuServicer _appContextMenuService;
    private readonly ICategorys _categoryService;
    private readonly INavigationService _navigationService;
    private int _appCount;
    private double _totalTime;

    public ChartPageViewModel(
        IData data,
        ICategorys categories,
        INavigationService navigationService,
        IWebData webData,
        IWebSiteContextMenuServicer webSiteContextMenu,
        IAppContextMenuServicer appContextMenu,
        IAppConfig appConfig)
    {
        _dataService = data;
        _categoryService = categories;
        _navigationService = navigationService;
        _appContextMenuService = appContextMenu;
        _webDataService = webData;
        _webSiteContextMenuService = webSiteContextMenu;
        _config = appConfig.GetConfig();

        ToDetailCommand = ReactiveCommand.Create<object>(OnToDetail);
        RefreshCommand = ReactiveCommand.CreateFromTask<object>(OnRefreshAsync);
        ToDetailCommand.DisposeWith(Disposables);
        RefreshCommand.DisposeWith(Disposables);

        Initialize();
    }

    public ReactiveCommand<object, Unit> ToDetailCommand { get; }
    public ReactiveCommand<object, Unit> RefreshCommand { get; }

    public List<SelectItemModel> PeriodOptions { get; } =
    [
        new() { Id = 0, Name = ResourceStrings.Daily },
        new() { Id = 1, Name = ResourceStrings.Weekly },
        new() { Id = 2, Name = ResourceStrings.Monthly },
        new() { Id = 3, Name = ResourceStrings.Yearly }
    ];

    public List<SelectItemModel> ChartDataModeOptions { get; } =
    [
        new() { Id = 1, Name = ResourceStrings.CategoryView },
        new() { Id = 2, Name = ResourceStrings.SummaryView }
    ];

    private void Initialize()
    {
        TabbarData = [ResourceStrings.Daily, ResourceStrings.Weekly, ResourceStrings.Monthly, ResourceStrings.Yearly];

        ChartDataMode = ChartDataModeOptions[0];
        ShowType = ShowTypeOptions[0];
        SelectedPeriod = PeriodOptions[0];
        Date = DateTime.Now;
        WeekDate = DateTime.Now;
        MonthDate = DateTime.Now;
        YearDate = DateTime.Now;
        TabbarSelectedIndex = 0;
        AppContextMenu = _appContextMenuService.GetContextMenu();
        WebSiteContextMenu = _webSiteContextMenuService.GetContextMenu();

        WhenPropertyChanged(this, x => x.Date, _ => OnDateChangedAsync());
        WhenPropertyChanged(this, x => x.TabbarSelectedIndex, _ => OnTabbarChangedAsync());
        WhenPropertyChanged(this, x => x.WeekDate, _ => OnWeekDateChangedAsync());
        WhenPropertyChanged(this, x => x.MonthDate, _ => OnMonthDateChangedAsync());
        WhenPropertyChanged(this, x => x.YearDate, _ => OnYearDateChangedAsync());
        WhenPropertyChanged(this, x => x.ShowType, _ => OnShowTypeChangedAsync());
        this.WhenAnyValue(x => x.ColumnSelectedIndex)
            .ObserveOn(AvaloniaScheduler.Instance)
            .Select(_ => Observable.FromAsync(async ct => await LoadSelectedColDataAsync(ct)))
            .Switch()
            .Subscribe()
            .DisposeWith(Disposables);

        WhenPropertyChanged(this, x => x.ChartDataMode, _ => OnChartDataModeChangedAsync());
        WhenPropertyChanged(this, x => x.SelectedPeriod, p =>
        {
            if (p != null) TabbarSelectedIndex = p.Id;
            return Task.CompletedTask;
        });
        this.WhenAnyValue(x => x.WebColSelectedIndex)
            .ObserveOn(AvaloniaScheduler.Instance)
            .Select(_ => Observable.FromAsync(async ct => await LoadWebSitesColSelectedDataAsync(ct)))
            .Switch()
            .Subscribe()
            .DisposeWith(Disposables);
    }

    public override Task OnNavigatedToAsync()
    {
        _ = ExecuteAsync(LoadDataAsync);
        return Task.CompletedTask;
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

    private Task OnChartDataModeChangedAsync() => ExecuteAsync(async ct =>
    {
        IsChartStack = ChartDataMode.Id == 1;
        await LoadDataAsync(ct);
    });

    private Task OnShowTypeChangedAsync() => ExecuteAsync(LoadDataAsync);

    private Task OnRefreshAsync(object _) => ExecuteAsync(LoadDataAsync);



    private void OnToDetail(object obj)
    {
        if (obj is not ChartsDataModel chartData) return;

        if (chartData.Data is WebSiteModel webSite)
        {
            _navigationService.NavigateTo(nameof(WebSiteDetailPage), webSite);
        }
        else if (chartData.Data is DailyLogModel { AppModel: not null } daily)
        {
            _navigationService.NavigateTo(nameof(DetailPage), daily.AppModel);
        }

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
            DataMaximum = 3600;
            var chartData = await BuildCategoryChartDataAsync(
                ct => _dataService.GetCategoryHoursDataAsync(Date, ct),
                ct => _dataService.GetRangeTotalDataAsync(Date, Date, ct),
                null,
                cancellationToken);

            var totalUse = chartData.data.Sum(m => m.Values.Sum());
            _totalTime = totalUse;
            Data = chartData.data;
            RadarData = chartData.categoryData;
            ColumnSelectedIndex = -1;
            WebColSelectedIndex = -1;
            TotalHours = Time.ToHoursString(totalUse);

            await LoadTopDataAsync(cancellationToken);
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

            var chartData = await BuildCategoryChartDataAsync(
                ct => _dataService.GetCategoryRangeDataAsync(weekDateArr.Start, weekDateArr.End, ct),
                ct => _dataService.GetRangeTotalDataAsync(weekDateArr.Start, weekDateArr.End, ct),
                weekNames,
                cancellationToken);

            var totalUse = chartData.data.Sum(m => m.Values.Sum());
            _totalTime = totalUse;
            Data = chartData.data;
            RadarData = chartData.categoryData;
            TotalHours = Time.ToHoursString(totalUse);

            await LoadTopDataAsync(cancellationToken);
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
            var chartData = await BuildCategoryChartDataAsync(
                ct => _dataService.GetCategoryRangeDataAsync(dateArr[0], dateArr[1], ct),
                ct => _dataService.GetRangeTotalDataAsync(dateArr[0], dateArr[1], ct),
                null,
                cancellationToken);

            var totalUse = chartData.data.Sum(m => m.Values.Sum());
            _totalTime = totalUse;
            Data = chartData.data;
            RadarData = chartData.categoryData;
            TotalHours = Time.ToHoursString(totalUse);

            await LoadTopDataAsync(cancellationToken);
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
            var chartData = await BuildCategoryChartDataAsync(
                ct => _dataService.GetCategoryYearDataAsync(YearDate, ct),
                ct => _dataService.GetMonthTotalDataAsync(YearDate, ct),
                names,
                cancellationToken);

            var totalUse = chartData.data.Sum(m => m.Values.Sum());
            _totalTime = totalUse;
            Data = chartData.data;
            RadarData = chartData.categoryData;
            TotalHours = Time.ToHoursString(totalUse);

            await LoadTopDataAsync(cancellationToken);
        }
        else
        {
            var yearArr = Time.GetYearDate(YearDate);
            await LoadWebDataAsync(yearArr[0], yearArr[1], cancellationToken);
        }
    }

    private async Task<(List<ChartsDataModel> data, List<ChartsDataModel> categoryData)> BuildCategoryChartDataAsync(
        Func<CancellationToken, Task<IReadOnlyList<ColumnDataModel>>> getCategoryDataAsync,
        Func<CancellationToken, Task<double[]>> getTotalDataAsync,
        string[]? columnNames,
        CancellationToken cancellationToken)
    {
        var list = await getCategoryDataAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        var categoryData = new List<ChartsDataModel>();
        foreach (var item in list)
        {
            var category = await _categoryService.GetCategoryAsync(item.CategoryID, cancellationToken);
            category ??= new CategoryModel { Name = "Unknown", Color = "#ccc", IconFile = "" };
            categoryData.Add(new ChartsDataModel
            {
                Name = category.Name,
                Icon = category.IconFile,
                Values = item.Values,
                Color = category.Color,
                ColumnNames = columnNames
            });
        }

        // 按分类总时长降序排序
        categoryData = categoryData.OrderByDescending(c => c.Values.Sum()).ToList();

        List<ChartsDataModel> data;
        if (ChartDataMode.Id == 1)
        {
            data = categoryData;
        }
        else
        {
            var values = await getTotalDataAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
#pragma warning disable CS8601
            data =
            [
                new ChartsDataModel
                {
                    Values = values,
                    ColumnNames = columnNames
                }
            ];
#pragma warning restore CS8601
        }

        return (data, categoryData);
    }

    private async Task LoadTopDataAsync(CancellationToken cancellationToken)
    {
        var dateStart = Date.Date;
        var dateEnd = Date.Date;
        if (TabbarSelectedIndex == 1)
        {
            var weekDateArr = GetWeekRange(WeekDate);
            dateStart = weekDateArr.Start;
            dateEnd = weekDateArr.End;
        }
        else if (TabbarSelectedIndex == 2)
        {
            var dateArr = Time.GetMonthDate(MonthDate);
            dateStart = dateArr[0];
            dateEnd = dateArr[1];
        }
        else if (TabbarSelectedIndex == 3)
        {
            var dateArr = Time.GetYearDate(YearDate);
            dateStart = dateArr[0];
            dateEnd = dateArr[1];
        }

        var list = await _dataService.GetDateRangelogListAsync(dateStart, dateEnd, 5, -1, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        TopData = ChartDataMapper.MapFromDailyLogs(list);
        TopHours = TopData.Count > 0 ? Time.ToHoursString(TopData[0].Value) : "0";

        _appCount = await _dataService.GetDateRangeAppCountAsync(dateStart, dateEnd, cancellationToken);
        AppCount = _appCount.ToString();
        Top1App = TopData.Count > 0 && TopData[0].Data is DailyLogModel { AppModel: not null } model ? model.AppModel : null;

        await LoadDiffDataAsync(dateStart, dateEnd, cancellationToken);
    }

    private async Task LoadDiffDataAsync(DateTime dateStart, DateTime dateEnd, CancellationToken cancellationToken)
    {
        var lastStart = dateStart.AddDays(-1);
        var lastEnd = dateEnd.AddDays(-1);
        if (TabbarSelectedIndex == 1)
        {
            var weekDateArr = GetWeekRange(WeekDate.AddDays(-7));
            lastStart = weekDateArr.Start;
            lastEnd = weekDateArr.End;
        }
        else if (TabbarSelectedIndex == 2)
        {
            var dateArr = Time.GetMonthDate(MonthDate.AddMonths(-1));
            lastStart = dateArr[0];
            lastEnd = dateArr[1];
        }
        else if (TabbarSelectedIndex == 3)
        {
            var dateArr = Time.GetYearDate(YearDate.AddYears(-1));
            lastStart = dateArr[0];
            lastEnd = dateArr[1];
        }

        var lastAppCount = await _dataService.GetDateRangeAppCountAsync(lastStart, lastEnd, cancellationToken);
        var lastTotalTime = (await _dataService.GetRangeTotalDataAsync(lastStart, lastEnd, cancellationToken)).Sum();
        cancellationToken.ThrowIfCancellationRequested();

        var diffTotalTime = lastTotalTime == 0
            ? (_totalTime > 0 ? 100 : 0)
            : (_totalTime - lastTotalTime) / lastTotalTime * 100;

        DiffTotalTimeType = diffTotalTime > 0 ? "1" : diffTotalTime < 0 ? "-1" : "0";
        DiffTotalTimeValue = DiffTotalTimeType == "0" ? string.Empty :
            diffTotalTime >= 100 ? "100%" : Math.Abs(diffTotalTime).ToString("f2") + "%";

        var diffAppCount = _appCount - lastAppCount;
        DiffAppCountType = diffAppCount > 0 ? "1" : diffAppCount < 0 ? "-1" : "0";
        DiffAppCountValue = DiffAppCountType == "0" ? string.Empty : Math.Abs(diffAppCount).ToString();

        LastWebTotalTime = await _webDataService.GetBrowseDurationTotalAsync(lastStart, lastEnd, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        LastWebSiteCount = await _webDataService.GetBrowseSitesTotalAsync(lastStart, lastEnd, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        LastWebPageCount = await _webDataService.GetBrowsePagesTotalAsync(lastStart, lastEnd, cancellationToken);
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
            chartsDatas = ChartDataMapper.MapFromDailyLogs(daysModelList, true);
        }

        cancellationToken.ThrowIfCancellationRequested();
        DayHoursData = chartsDatas;
    }

    #region 网页数据

    private async Task LoadWebDataAsync(DateTime start, DateTime end, CancellationToken cancellationToken)
    {
        var categories = await _webDataService.GetWebSiteCategoriesAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        await Task.WhenAll(
            LoadCategoriesStatisticsAsync(start, end, categories, cancellationToken),
            LoadWebSitesTopDataAsync(start, end, cancellationToken),
            LoadWebBrowseDataStatisticsAsync(start, end, categories, cancellationToken));
        cancellationToken.ThrowIfCancellationRequested();
        WebTotalTime = await _webDataService.GetBrowseDurationTotalAsync(start, end, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        WebSiteCount = await _webDataService.GetBrowseSitesTotalAsync(start, end, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        WebPageCount = await _webDataService.GetBrowsePagesTotalAsync(start, end, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        WebTotalTimeText = Time.ToHoursString(WebTotalTime);
    }

    private async Task LoadCategoriesStatisticsAsync(DateTime start, DateTime end, IReadOnlyList<WebSiteCategoryModel> categories, CancellationToken cancellationToken)
    {
        var chartsDatas = new List<ChartsDataModel>();
        var data = await _webDataService.GetCategoriesStatisticsAsync(start, end, cancellationToken);
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
        cancellationToken.ThrowIfCancellationRequested();
        WebCategoriesPieData = chartsDatas.OrderByDescending(m => m.Value).ToList();
    }

    private async Task LoadWebBrowseDataStatisticsAsync(DateTime start, DateTime end, IReadOnlyList<WebSiteCategoryModel> categories, CancellationToken cancellationToken)
    {
        var chartData = new List<ChartsDataModel>();
        var data = await _webDataService.GetBrowseDataByCategoryStatisticsAsync(start, end, cancellationToken);
        var categoryDict = categories.ToDictionary(c => c.ID);
        var emptyCategory = new WebSiteCategoryModel
        {
            ID = 0,
            Name = "Unknown",
            IconFile = ""
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
        cancellationToken.ThrowIfCancellationRequested();
        WebBrowseStatisticsData = chartData.OrderByDescending(m => m.Values.Sum()).ToList();
    }

    private async Task LoadWebSitesTopDataAsync(DateTime start, DateTime end, CancellationToken cancellationToken)
    {
        var data = await _webDataService.GetDateRangeWebSiteListAsync(start, end, 10, cancellationToken: cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        WebSitesTopData = ChartDataMapper.MapFromWebSites(data);
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

        var chartData = ChartDataMapper.MapFromWebSites(await _webDataService.GetDateRangeWebSiteListAsync(startTime, endTime, 0, -1, isTime, cancellationToken));
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

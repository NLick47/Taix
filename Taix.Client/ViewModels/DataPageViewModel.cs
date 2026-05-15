using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables.Fluent;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Styling;
using Avalonia.Threading;
using ReactiveUI;
using Taix.Client.Base.Color;
using Taix.Client.Controls.Charts.Model;
using Taix.Client.Controls.Select;
using Taix.Client.Controls.Timeline;
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


    private List<AppSessionModel> _daySessions = [];
    private List<ChartsDataModel> _dayAppRawData = [];
    private CancellationTokenSource? _updateFilterCts;

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
        RefreshCommand = ReactiveCommand.CreateFromTask<object>(OnRefreshAsync);
        RefreshCommand.DisposeWith(Disposables);
        Initialize();
    }

    public ICommand ToDetailCommand { get; }
    public ReactiveCommand<object, Unit> RefreshCommand { get; }

    public List<SelectItemModel> PeriodOptions { get; } =
    [
        new() { Id = 0, Name = ResourceStrings.Daily },
        new() { Id = 1, Name = ResourceStrings.Weekly },
        new() { Id = 2, Name = ResourceStrings.Monthly },
        new() { Id = 3, Name = ResourceStrings.Yearly }
    ];

    public override void Dispose()
    {
        (ToDetailCommand as IDisposable)?.Dispose();
        base.Dispose();
    }

    private void Initialize()
    {
        TabbarData = [ResourceStrings.Daily, ResourceStrings.Weekly, ResourceStrings.Monthly, ResourceStrings.Yearly];
        AppContextMenu = _appContextMenuService.GetContextMenu();

        DayDate = DateTime.Now.Date;
        WeekDate = DateTime.Now.Date;
        MonthDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
        YearDate = new DateTime(DateTime.Now.Year, 1, 1);
        TimelineStartHour = 0.0;
        TimelineEndHour = 24.0;

        WhenPropertyChanged(this, x => x.DayDate, date => OnDateChangedAsync(date, 0));
        WhenPropertyChanged(this, x => x.WeekDate, date => OnDateChangedAsync(date, 1));
        WhenPropertyChanged(this, x => x.MonthDate, date => OnDateChangedAsync(date, 2));
        WhenPropertyChanged(this, x => x.YearDate, date => OnDateChangedAsync(date, 3));

        WhenPropertyChanged(this, x => x.SelectedPeriod, p =>
        {
            if (p != null) TabbarSelectedIndex = p.Id;
            return Task.CompletedTask;
        });

        WhenPropertyChanged(this, x => x.TabbarSelectedIndex, async index =>
        {
            if (index == 0 && DayDate == DateTime.MinValue)
                DayDate = DateTime.Now.Date;
            else if (index == 1 && WeekDate == DateTime.MinValue)
                WeekDate = DateTime.Now.Date;
            else if (index == 2 && MonthDate == DateTime.MinValue)
                MonthDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            else if (index == 3 && YearDate == DateTime.MinValue)
                YearDate = new DateTime(DateTime.Now.Year, 1, 1);
            await Task.CompletedTask;
        }, skipInitial: false);

        WhenPropertyChanged(this, x => x.ShowType, async _ =>
        {
            await LoadDataAsync(DayDate, 0);
            await LoadDataAsync(WeekDate, 1);
            await LoadDataAsync(MonthDate, 2);
            await LoadDataAsync(YearDate, 3);
            AppContextMenu = ShowType.Id == 0
                ? _appContextMenuService.GetContextMenu()
                : _webSiteContextMenuService.GetContextMenu();
        });

        WhenPropertyChanged(this, x => x.TimelineStartHour, async _ => await DebounceUpdateFilteredDataAsync());
        WhenPropertyChanged(this, x => x.TimelineEndHour, async _ => await DebounceUpdateFilteredDataAsync());

        SelectedPeriod = PeriodOptions[0];
        TabbarSelectedIndex = 0;
    }


    public override Task OnNavigatedToAsync()
    {
        _ = LoadDataAsync(DayDate, 0);
        return Task.CompletedTask;
    }

    private Task OnDateChangedAsync(DateTime date, int dataType)
    {
        return LoadDataAsync(date, dataType);
    }

    private Task OnRefreshAsync(object _)
    {
        var (date, dataType) = TabbarSelectedIndex switch
        {
            0 => (DayDate, 0),
            1 => (WeekDate, 1),
            2 => (MonthDate, 2),
            3 => (YearDate, 3),
            _ => (DayDate, 0)
        };
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
            List<ChartsDataModel> chartData;
            if (ShowType.Id == 0)
            {
                var result = await _dataService.GetDateRangelogListAsync(start, end, cancellationToken: ct);
                ct.ThrowIfCancellationRequested();
                chartData = await Task.Run(() => ChartDataMapper.MapFromDailyLogs(result, includeBadges: true), ct);

                if (dataType == 0)
                {
                    _dayAppRawData = chartData;
                    await LoadTimelineDataAsync(date, ct);
                }
            }
            else
            {
                var result = await _webDataService.GetWebSiteLogListAsync(start, end, ct);
                ct.ThrowIfCancellationRequested();
                chartData = await Task.Run(() => ChartDataMapper.MapFromWebSites(result, includeBadges: true), ct);
            }

            ct.ThrowIfCancellationRequested();

            switch (dataType)
            {
                case 0:
                    if (ShowType.Id == 0)
                    {
                        await UpdateFilteredDataAsync();
                    }
                    else
                    {
                        Data = chartData;
                        TimelineUsageItems = [];
                        MultiTrackItems = [];
                        _daySessions = [];
                        _dayAppRawData = [];
                    }
                    break;
                case 1: WeekData = chartData; break;
                case 2: MonthData = chartData; break;
                case 3: YearData = chartData; break;
            }
        });
    }

    #region 时间轴

    private async Task LoadTimelineDataAsync(DateTime date, CancellationToken ct)
    {
        var dayStart = date.Date;
        var dayEnd = dayStart.AddDays(1).AddSeconds(-1);

        var sessions = await _dataService.GetAppSessionsAsync(dayStart, dayEnd, ct);
        ct.ThrowIfCancellationRequested();
        _daySessions = sessions.ToList();
        foreach (var s in _daySessions)
        {
            s.StartTime = DateTime.SpecifyKind(s.StartTime, DateTimeKind.Utc).ToLocalTime();
            s.EndTime = DateTime.SpecifyKind(s.EndTime, DateTimeKind.Utc).ToLocalTime();
        }

        var isDark = Application.Current?.ActualThemeVariant == ThemeVariant.Dark;
        var daySessions = _daySessions;

        var items = await Task.Run(() =>
        {
            var list = new List<TimelineUsageItem>();
            DateTime? cursor = null;
            foreach (var session in daySessions.Where(s => s.AppModel != null).OrderBy(s => s.StartTime))
            {
                if (cursor.HasValue && session.StartTime > cursor.Value)
                {
                    list.Add(new TimelineUsageItem
                    {
                        Name = ResourceStrings.IdleTime, Color = "#484f58",
                        CategoryName = ResourceStrings.SystemCategory, CategoryColor = "#8b949e",
                        Start = cursor.Value, End = session.StartTime,
                        IsIdle = true
                    });
                }

                var appName = session.AppModel?.Name ?? "Unknown";
                var paletteColor = Colors.GetTimelinePaletteColor(appName, isDark);
                var isShort = session.Duration <= 60;
                list.Add(new TimelineUsageItem
                {
                    Name = appName,
                    Color = paletteColor,
                    CategoryName = session.AppModel?.Category?.Name ?? "Unknown",
                    CategoryColor = paletteColor,
                    Start = session.StartTime,
                    End = session.EndTime,
                    Duration = session.Duration,
                    IsShortSession = isShort,
                    Data = session
                });
                cursor = session.EndTime;
            }

            if (cursor.HasValue && cursor.Value < dayEnd)
            {
                list.Add(new TimelineUsageItem
                {
                    Name = ResourceStrings.IdleTime, Color = "#484f58",
                    CategoryName = ResourceStrings.SystemCategory, CategoryColor = "#8b949e",
                    Start = cursor.Value, End = dayEnd,
                    IsIdle = true
                });
            }

            if (list.Count == 0)
            {
                list.Add(new TimelineUsageItem
                {
                    Name = ResourceStrings.IdleTime, Color = "#484f58",
                    CategoryName = ResourceStrings.SystemCategory, CategoryColor = "#8b949e",
                    Start = dayStart, End = dayEnd,
                    IsIdle = true
                });
            }

            return list;
        }, ct);

        TimelineUsageItems = items;

        if (TimelineStartHour == 0.0 && TimelineEndHour == 24.0)
        {
            var active = items.Where(i => !TimelineHelpers.IsIdleItem(i)).ToList();
            if (active.Count > 0)
            {
                var dataStart = active.Min(i => i.Start.TimeOfDay.TotalHours);
                var dataEnd = active.Max(i => i.End.TimeOfDay.TotalHours);
                var pad = 0.5;
                TimelineStartHour = Math.Max(0, Math.Floor((dataStart - pad) * 12) / 12);
                TimelineEndHour = Math.Min(24, Math.Ceiling((dataEnd + pad) * 12) / 12);
            }
        }
    }

    private async Task DebounceUpdateFilteredDataAsync()
    {
        _updateFilterCts?.Cancel();
        _updateFilterCts?.Dispose();
        _updateFilterCts = new CancellationTokenSource();
        var token = _updateFilterCts.Token;
        try
        {
            await Task.Delay(50, token);
            if (!token.IsCancellationRequested)
                await UpdateFilteredDataAsync();
        }
        catch (OperationCanceledException)
        {
            // ignored
        }
    }

    private async Task UpdateFilteredDataAsync()
    {
        // 仅应用模式支持按时间轴范围过滤
        if (ShowType.Id != 0)
        {
            MultiTrackItems = [];
            return;
        }

        if (_daySessions.Count == 0)
        {
            Data = _dayAppRawData;
            MultiTrackItems = [];
            return;
        }

        var startHour = Math.Min(TimelineStartHour, TimelineEndHour);
        var endHour = Math.Max(TimelineStartHour, TimelineEndHour);
        var startTime = DayDate.Date.AddHours(startHour);
        var endTime = DayDate.Date.AddHours(endHour);

        var timelineItems = TimelineUsageItems;
        var daySessions = _daySessions;

        var result = await Task.Run(() =>
        {
            var rangeSec = (endTime - startTime).TotalSeconds;
            var thresholdSec = rangeSec < 3600 ? 10 : 60;

            var filtered = daySessions
                .Where(s => s.AppModel != null && s.Duration > thresholdSec && s.EndTime > startTime && s.StartTime < endTime)
                .ToList();

            if (filtered.Count == 0)
            {
                return (Data: new List<ChartsDataModel>(), MultiTrackItems: new List<MultiTrackTimelineItem>(), TotalDurationText: "");
            }

            var grouped = filtered
                .GroupBy(s => s.AppModel!.ID)
                .Select(g => new DailyLogModel
                {
                    AppModel = g.First().AppModel,
                    Time = g.Sum(s => s.Duration),
                    Date = DayDate
                });

            var chartData = ChartDataMapper.MapFromDailyLogs(grouped, includeBadges: true);

            var (mtItems, durationText) = BuildMultiTrackItemsInternal(timelineItems, startTime, endTime);

            return (Data: chartData, MultiTrackItems: mtItems, TotalDurationText: durationText);
        });

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            Data = result.Data;
            MultiTrackItems = result.MultiTrackItems;
            MultiTrackTotalDurationText = result.TotalDurationText;
        }, DispatcherPriority.Background);
    }

    private static (List<MultiTrackTimelineItem> Items, string TotalDurationText) BuildMultiTrackItemsInternal(
        IEnumerable<TimelineUsageItem>? timelineItems, DateTime startTime, DateTime endTime)
    {
        // 非空闲 session 计入总时长
        var allItems = timelineItems?
            .Where(i => !TimelineHelpers.IsIdleItem(i) && i.End > startTime && i.Start < endTime)
            .ToList();

        if (allItems == null || allItems.Count == 0)
        {
            return ([], "");
        }

        double totalDurationSec = allItems.Sum(i => i.Duration);
        var totalDurationText = FormatDuration(totalDurationSec);

        // 动态阈值：<1h 显示 >10s，>=1h 显示 >60s
        var rangeSec = (endTime - startTime).TotalSeconds;
        var thresholdSec = rangeSec < 3600 ? 10 : 60;
        var longItems = allItems.Where(i => i.Duration > thresholdSec).ToList();

        var result = longItems
            .GroupBy(i => i.Name)
            .Select(g =>
            {
                var first = g.First();
                var segments = g.Select(seg => new MultiTrackSegment
                {
                    Start = seg.Start < startTime ? startTime : seg.Start,
                    End = seg.End > endTime ? endTime : seg.End,
                    Color = seg.Color
                }).ToList();

                var appDurationSec = g.Sum(seg => seg.Duration);
                var appModel = (first.Data as AppSessionModel)?.AppModel;
                var percentage = totalDurationSec > 0 ? appDurationSec / totalDurationSec * 100 : 0;

                return new MultiTrackTimelineItem
                {
                    Name = first.Name,
                    Icon = appModel?.IconFile,
                    Color = first.Color,
                    CategoryName = first.CategoryName,
                    CategoryColor = first.CategoryColor,
                    TotalDuration = TimeSpan.FromSeconds(appDurationSec),
                    Percentage = percentage,
                    Segments = segments
                };
            })
            .OrderByDescending(i => i.TotalDuration)
            .ToList();

        return (result, totalDurationText);
    }

    private static bool IsIdleItem(TimelineUsageItem item) =>
        TimelineHelpers.IsIdleItem(item.Name);

    #endregion

    private static (DateTime Start, DateTime End) GetDateRange(DateTime date, int dataType)
    {
        return dataType switch
        {
            0 => (date.Date, date.Date.AddDays(1).AddTicks(-1)),
            1 => GetWeekRange(date),
            2 => (new DateTime(date.Year, date.Month, 1), new DateTime(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month), 23, 59, 59)),
            3 => (new DateTime(date.Year, 1, 1), new DateTime(date.Year, 12, 31, 23, 59, 59)),
            _ => (date, date)
        };
    }

    private static (DateTime Start, DateTime End) GetWeekRange(DateTime date)
    {
        var dayOfWeek = (int)date.DayOfWeek;
        dayOfWeek = dayOfWeek == 0 ? 7 : dayOfWeek;
        var start = date.Date.AddDays(-(dayOfWeek - 1));
        var end = start.AddDays(6).Date.AddHours(23).AddMinutes(59).AddSeconds(59);
        return (start, end);
    }

    private static string FormatDuration(double totalSeconds)
    {
        var ts = TimeSpan.FromSeconds(totalSeconds);
        if (ts.TotalMinutes < 1)
            return $"{ts.Seconds}s";
        if (ts.TotalHours >= 1)
            return $"{(int)ts.TotalHours}h {ts.Minutes}m";
        return $"{ts.Minutes}m";
    }
}

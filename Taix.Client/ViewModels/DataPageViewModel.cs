using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Styling;
using Avalonia.Threading;
using ReactiveUI;
using ReactiveUI.Avalonia;
using Taix.Client.Base.Color;
using Taix.Client.Controls.Charts.Model;
using Taix.Client.Controls.Timeline;
using Taix.Client.Events;
using Taix.Client.Librarys;
using Taix.Client.Models;
using Taix.Client.Models.Navigation;
using Taix.Client.Servicers;
using Taix.Client.Servicers.Interfaces;
using Taix.Client.Shared.Models;
using Taix.Client.Shared.Models.Web;
using Taix.Client.Shared.Servicers.Interfaces;
using Taix.Client.Views;
using AppModel = Taix.Client.Shared.Models.AppModel;

namespace Taix.Client.ViewModels;

public partial class DataPageViewModel : DataPageModel
{
    private readonly IWebData _webDataService;
    private readonly IAppConfig _appConfig;
    private readonly IData _dataService;
    private readonly INavigationService _navigationService;
    private readonly IStateService _stateService;
    private readonly IAppEventService _appEventService;


    private List<ChartsDataModel> _dayAppRawData = [];
    private CancellationTokenSource? _updateFilterCts;
    private bool _suppressTimelineFilterUpdate;

    public DataPageViewModel(
        IData data,
        IAppConfig appConfig,
        IWebData webData,
        INavigationService navigationService,
        IStateService stateService,
        IAppEventService appEventService)
    {
        _dataService = data;
        _appConfig = appConfig;
        _webDataService = webData;
        _navigationService = navigationService;
        _stateService = stateService;
        _appEventService = appEventService;

        ToDetailCommand = ReactiveCommand.Create<object>(OnToDetail);
        RefreshCommand = ReactiveCommand.CreateFromTask<object>(OnRefreshAsync);
        RefreshCommand.DisposeWith(Disposables);
        Initialize();

        _appEventService.AppChanged
            .Throttle(TimeSpan.FromMilliseconds(100))
            .ObserveOn(AvaloniaScheduler.Instance)
            .Subscribe(async _ => await RefreshAsync())
            .DisposeWith(Disposables);

        _appEventService.WebSiteChanged
            .Throttle(TimeSpan.FromMilliseconds(100))
            .ObserveOn(AvaloniaScheduler.Instance)
            .Subscribe(async _ => await RefreshAsync())
            .DisposeWith(Disposables);
    }

    public ICommand ToDetailCommand { get; }
    public ReactiveCommand<object, Unit> RefreshCommand { get; }

    public override void Dispose()
    {
        (ToDetailCommand as IDisposable)?.Dispose();
        base.Dispose();
    }

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
            DateTime date;
            int dataType;
            if (index == 0)
            {
                if (DayDate == DateTime.MinValue) DayDate = DateTime.Now.Date;
                (date, dataType) = (DayDate, 0);
            }
            else if (index == 1)
            {
                if (WeekDate == DateTime.MinValue) WeekDate = DateTime.Now.Date;
                (date, dataType) = (WeekDate, 1);
            }
            else if (index == 2)
            {
                if (MonthDate == DateTime.MinValue) MonthDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                (date, dataType) = (MonthDate, 2);
            }
            else if (index == 3)
            {
                if (YearDate == DateTime.MinValue) YearDate = new DateTime(DateTime.Now.Year, 1, 1);
                (date, dataType) = (YearDate, 3);
            }
            else
            {
                return;
            }
            await LoadDataAsync(date, dataType);
        }, skipInitial: false);

        WhenPropertyChanged(this, x => x.ShowType, async _ =>
        {
            await LoadDataAsync(DayDate, 0);
            await LoadDataAsync(WeekDate, 1);
            await LoadDataAsync(MonthDate, 2);
            await LoadDataAsync(YearDate, 3);
        });

        WhenPropertyChanged(this, x => x.TimelineStartHour, async _ =>
        {
            if (!_suppressTimelineFilterUpdate)
                await DebounceUpdateFilteredDataAsync();
        });
        WhenPropertyChanged(this, x => x.TimelineEndHour, async _ =>
        {
            if (!_suppressTimelineFilterUpdate)
                await DebounceUpdateFilteredDataAsync();
        });

        SelectedPeriod = PeriodOptions[0];
        SelectedColorMode = ColorModeOptions[0];
        TabbarSelectedIndex = 0;
    }


    public override async Task OnNavigatedToAsync()
    {
        TryRestoreState(_navigationService, _stateService);
        await LoadDataAsync(DayDate, 0);
    }

    public override void OnNavigatedFrom()
    {
        SaveState(_stateService);
        base.OnNavigatedFrom();
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

    public override Task RefreshAsync() => OnRefreshAsync(null!);

    private void OnToDetail(object obj)
    {
        var date = TabbarSelectedIndex switch { 0 => DayDate, 1 => WeekDate, 2 => MonthDate, 3 => YearDate, _ => DateTime.Now };

        switch (obj)
        {
            case MultiTrackTimelineItem { AppModel: AppModel app }:
                _navigationService.NavigateTo(nameof(DetailPage), DetailNavigationContext.Create(app, TabbarSelectedIndex, date));
                break;
            case ChartsDataModel { Data: DailyLogModel { AppModel: not null } model }:
                _navigationService.NavigateTo(nameof(DetailPage), DetailNavigationContext.Create(model.AppModel, TabbarSelectedIndex, date));
                break;
            case ChartsDataModel { Data: WebSiteModel webSite }:
                _navigationService.NavigateTo(nameof(WebSiteDetailPage), WebSiteDetailNavigationContext.Create(webSite, TabbarSelectedIndex, date));
                break;
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
                        DaySessions = [];
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
        DaySessions = sessions.ToList();

        var isDark = Application.Current?.ActualThemeVariant == ThemeVariant.Dark;
        var daySessions = DaySessions;

        var items = await Task.Run(() =>
        {
            var list = new List<TimelineUsageItem>();
            DateTime? cursor = null;
            foreach (var session in daySessions.Where(s => s.AppModel != null).OrderBy(s => s.StartTime))
            {
                var startLocal = DateTime.SpecifyKind(session.StartTime, DateTimeKind.Utc).ToLocalTime();
                var endLocal = DateTime.SpecifyKind(session.EndTime, DateTimeKind.Utc).ToLocalTime();

                if (cursor.HasValue && startLocal > cursor.Value)
                {
                    list.Add(new TimelineUsageItem
                    {
                        Name = ResourceStrings.IdleTime, Color = "#484f58",
                        CategoryName = ResourceStrings.SystemCategory, CategoryColor = "#8b949e",
                        Start = cursor.Value, End = startLocal,
                        IsIdle = true
                    });
                }

                var appName = session.AppModel?.GetDisplayName() ?? "Unknown";
                var categoryName = session.AppModel?.Category?.Name ?? "Unknown";
                var paletteColor = TimelineColorService.GetColor(appName, isDark);
                var categoryColor = session.AppModel?.Category?.Color
                                    ?? TimelineColorService.GetColor(categoryName, isDark);
                list.Add(new TimelineUsageItem
                {
                    Name = appName,
                    Color = paletteColor,
                    CategoryName = categoryName,
                    CategoryColor = categoryColor,
                    Start = startLocal,
                    End = endLocal,
                    Data = session
                });
                cursor = endLocal;
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
        if (ShowType.Id != 0)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                MultiTrackItems = [];
                MultiTrackTotalDurationText = "";
            });
            return;
        }

        if (DaySessions.Count == 0)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Data = _dayAppRawData;
                MultiTrackItems = [];
                MultiTrackTotalDurationText = "";
            });
            return;
        }

        var referenceDate = DayDate.Date;
        var startHour = Math.Min(TimelineStartHour, TimelineEndHour);
        var endHour = Math.Max(TimelineStartHour, TimelineEndHour);
        var startTime = referenceDate.AddHours(startHour);
        var endTime = referenceDate.AddHours(endHour);

        var daySessions = DaySessions;
        var isDark = Application.Current?.ActualThemeVariant == ThemeVariant.Dark;
        var utcStart = startTime.ToUniversalTime();
        var utcEnd = endTime.ToUniversalTime();
        var result = await Task.Run(() =>
        {
            // Step 1: 过滤 + 剪裁 + 转本地时间
            var rawFiltered = daySessions
                .Where(s => s.AppModel != null && s.EndTime > utcStart && s.StartTime < utcEnd)
                .Select(s =>
                {
                    var clipStart = s.StartTime < utcStart ? utcStart : s.StartTime;
                    var clipEnd = s.EndTime > utcEnd ? utcEnd : s.EndTime;
                    var clipDuration = (int)(clipEnd - clipStart).TotalSeconds;
                    var localClipStart = ClipUtcToLocal(clipStart, referenceDate);
                    var localClipEnd = ClipUtcToLocal(clipEnd, referenceDate);
                    return (Session: s, ClipDuration: clipDuration, LocalStart: localClipStart, LocalEnd: localClipEnd);
                })
                .ToList();

            if (rawFiltered.Count == 0)
                return (Data: new List<ChartsDataModel>(), MultiTrackItems: new List<MultiTrackTimelineItem>(), TotalDurationText: "");

            // Step 2: 图表数据
            var chartData = ChartDataMapper.MapFromDailyLogs(
                rawFiltered.GroupBy(x => x.Session.AppModel!.ID)
                    .Select(g =>
                    {
                        var first = g.First();
                        return new DailyLogModel
                        {
                            AppModel = first.Session.AppModel,
                            Time = g.Sum(x => x.ClipDuration),
                            Date = DayDate
                        };
                    }),
                includeBadges: true);

            // Step 3: 多轨道数据
            var totalDurationSec = rawFiltered.Sum(x => x.ClipDuration);
            var totalDurationText = FormatDuration(totalDurationSec);
            var mtItems = BuildMultiTrackItems(rawFiltered, totalDurationSec, isDark);

            return (Data: chartData, MultiTrackItems: mtItems, TotalDurationText: totalDurationText);
        });

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            Data = result.Data;
            MultiTrackItems = result.MultiTrackItems;
            MultiTrackTotalDurationText = result.TotalDurationText;
        }, DispatcherPriority.Background);
    }

    private static List<MultiTrackTimelineItem> BuildMultiTrackItems(
        List<(AppSessionModel Session, int ClipDuration, DateTime LocalStart, DateTime LocalEnd)> rawFiltered,
        double totalDurationSec, bool isDark)
    {
        return rawFiltered
            .GroupBy(x => x.Session.AppModel!.GetDisplayName() ?? "Unknown")
            .Select(g =>
            {
                var first = g.First();
                var appModel = first.Session.AppModel;
                var paletteColor = TimelineColorService.GetColor(g.Key, isDark);

                var rawSegments = g
                    .Select(x => new MultiTrackSegment
                    {
                        Start = x.LocalStart,
                        End = x.LocalEnd,
                        Color = paletteColor,
                        DurationMinutes = (int)(x.LocalEnd - x.LocalStart).TotalMinutes,
                        ActualDurationSeconds = x.ClipDuration,
                    });

                var segments = MergeSegments(rawSegments);

                var appDurationSec = g.Sum(x => x.ClipDuration);

                return new MultiTrackTimelineItem
                {
                    Name = g.Key,
                    Icon = appModel?.IconFile,
                    Color = paletteColor,
                    TotalDuration = TimeSpan.FromSeconds(appDurationSec),
                    Segments = segments,
                    AppModel = appModel,
                };
            })
            .OrderByDescending(i => i.TotalDuration)
            .ToList();
    }

    private static List<MultiTrackSegment> MergeSegments(IEnumerable<MultiTrackSegment> source, int mergeGapSec = 60)
    {
        var sorted = source.OrderBy(s => s.Start).ToList();
        if (sorted.Count <= 1) return sorted;

        var merged = new List<MultiTrackSegment> { sorted[0] };

        for (var i = 1; i < sorted.Count; i++)
        {
            var cur = sorted[i];
            var last = merged[^1];
            var gap = (cur.Start - last.End).TotalSeconds;

            if (gap < mergeGapSec)
            {
                // 只扩展不缩小，处理重叠/相邻
                if (cur.End > last.End)
                {
                    last.End = cur.End;
                    last.DurationMinutes = (int)(last.End - last.Start).TotalMinutes;
                    last.ActualDurationSeconds += cur.ActualDurationSeconds;  // 累加实际使用时长
                }
            }
            else
            {
                merged.Add(cur);
            }
        }

        return merged;
    }

    private static bool IsIdleItem(TimelineUsageItem item) =>
        TimelineHelpers.IsIdleItem(item.Name);


    private static DateTime ClipUtcToLocal(DateTime utcTime, DateTime referenceDate)
    {
        var local = utcTime.ToLocalTime();
        return referenceDate.Date + local.TimeOfDay;
    }

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

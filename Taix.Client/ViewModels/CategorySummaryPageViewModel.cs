using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables.Fluent;
using System.Threading.Tasks;
using ReactiveUI;
using Taix.Client.Controls.Charts.Model;
using Taix.Client.Controls.Select;
using Taix.Client.Models;
using Taix.Client.Models.Navigation;
using Taix.Client.Servicers.Interfaces;
using Taix.Client.Shared.Librarys;
using Taix.Client.Shared.Models.Category;
using Taix.Client.Shared.Servicers.Interfaces;

namespace Taix.Client.ViewModels;

public class CategorySummaryPageViewModel : ModelBase
{
    private readonly ICategorySummaryData _summaryData;
    private readonly ICategorys _categorys;
    private readonly IWebData _webData;
    private readonly INavigationDataService _navigationData;

    // 内部存储分类列表
    private List<CategoryChip> _appCategoryList = [];
    private List<CategoryChip> _webCategoryList = [];

    private CategorySummaryKind _kind = CategorySummaryKind.App;
    private int _categoryId;
    private string _categoryName = string.Empty;
    private string? _categoryIcon;
    private string? _categoryColor;

    private SelectItemModel _selectedRange;
    private DateTime _selectedDate = DateTime.Today;
    private DateSelectType _currentSelectType = DateSelectType.Date;

    private long _totalSeconds;
    private int _activeDays;
    private long _averageDailySeconds;
    private double _peakSeconds;

    private string _totalText = string.Empty;
    private string _averageDailyText = string.Empty;
    private string _rangeLabel = string.Empty;

    private bool _hasData;

    // 柱状图 + 点击列展开成员
    private List<ChartsDataModel> _columnChartData = new();
    private int _columnSelectedIndex = -1;
    private bool _isCanColumnSelect = true;
    private List<ChartsDataModel> _selectedMembers = new();
    private string? _selectedColumnLabel;

    // 当前 range 的起止，供点击列算子区间用
    private DateTime _rangeStart;
    private DateTime _rangeEnd;

    // 切换 range/kind 重置选中列时短暂置位，避免 WhenPropertyChanged 重复触发加载
    private bool _suppressMemberLoad;

    // 环比
    private long _previousTotalSeconds;
    private string? _vsLabel = string.Empty;
    private string? _diffText = string.Empty;
    private bool _diffUp;
    private bool _diffDown;
    private bool _diffFlat;
    private bool _isDiffVisible;
    private string _peakText = string.Empty;
    private string _peakMetaText = string.Empty;

    public CategorySummaryPageViewModel(
        ICategorySummaryData summaryData,
        ICategorys categorys,
        IWebData webData,
        INavigationDataService navigationData)
    {
        _summaryData = summaryData;
        _categorys = categorys;
        _webData = webData;
        _navigationData = navigationData;

        BackCommand = ReactiveCommand.Create<object?>(OnBack).DisposeWith(Disposables);
        SwitchCategoryCommand = ReactiveCommand.CreateFromTask<CategoryChip>(OnSwitchCategoryAsync).DisposeWith(Disposables);

        Categories = new ObservableCollection<CategoryChip>();
        DailyTrend = new ObservableCollection<DailyPointModel>();

        // 点击柱状图列加载子区间成员，-1 为全选
        WhenPropertyChanged(this, x => x.ColumnSelectedIndex,
            _ => _suppressMemberLoad ? System.Threading.Tasks.Task.CompletedTask : LoadMembersAsync());

        RangeOptions = BuildRangeOptions();
        _selectedRange = RangeOptions[0]; // 默认今日
        _currentSelectType = RangeIdToSelectType(_selectedRange.Id);

        KindOptions = BuildKindOptions();
        _selectedKind = KindOptions[(int)Kind]; // 同步初始 Kind

        WhenPropertyChanged(this, x => x.SelectedRange, _ =>
        {
            CurrentSelectType = RangeIdToSelectType(SelectedRange.Id);
            return LoadAsync();
        });
        WhenPropertyChanged(this, x => x.SelectedDate, _ => LoadAsync());
    }

    public ReactiveCommand<object?, Unit> BackCommand { get; }
    public ReactiveCommand<CategoryChip, Unit> SwitchCategoryCommand { get; }

    public ObservableCollection<CategoryChip> Categories { get; }

    // 当前显示的分类列表（根据 Kind 切换）
    private ObservableCollection<CategoryChip> _currentCategories = new();
    public ObservableCollection<CategoryChip> CurrentCategories
    {
        get => _currentCategories;
        set
        {
            _currentCategories = value;
            OnPropertyChanged();
        }
    }

    // TabSwitch 选项
    public List<SelectItemModel> KindOptions { get; }

    private SelectItemModel _selectedKind;
    public SelectItemModel SelectedKind
    {
        get => _selectedKind;
        set
        {
            if (_selectedKind == value || value == null) return;
            _selectedKind = value;
            OnPropertyChanged();
            _ = OnKindChangedAsync();
        }
    }

    private ObservableCollection<DailyPointModel> _dailyTrend = new();
    public ObservableCollection<DailyPointModel> DailyTrend
    {
        get => _dailyTrend;
        set
        {
            _dailyTrend = value;
            OnPropertyChanged();
        }
    }


    // 0-23 小时分布柱状图（单系列 24 列）
    private List<ChartsDataModel> _hourlyChart = new();
    public List<ChartsDataModel> HourlyChart
    {
        get => _hourlyChart;
        set
        {
            _hourlyChart = value;
            OnPropertyChanged();
        }
    }

    // 分类类型：用于头部徽章区分应用分类/网站分类
    public bool IsAppKind => Kind == CategorySummaryKind.App;
    public bool IsWebKind => Kind == CategorySummaryKind.Web;

    public List<SelectItemModel> RangeOptions { get; }

    public SelectItemModel SelectedRange
    {
        get => _selectedRange;
        set
        {
            if (_selectedRange == value || value == null) return;
            _selectedRange = value;
            OnPropertyChanged();
        }
    }

    /// <summary>DateSelect 当前选中的日期，作为周期的锚点</summary>
    public DateTime SelectedDate
    {
        get => _selectedDate;
        set
        {
            if (_selectedDate == value) return;
            _selectedDate = value;
            OnPropertyChanged();
        }
    }

    /// <summary>DateSelect 的选择类型，随 SelectedRange 切换（日/周/月/年）</summary>
    public DateSelectType CurrentSelectType
    {
        get => _currentSelectType;
        set
        {
            if (_currentSelectType == value) return;
            _currentSelectType = value;
            OnPropertyChanged();
        }
    }

    public string CategoryName
    {
        get => _categoryName;
        set
        {
            _categoryName = value;
            OnPropertyChanged();
        }
    }

    public string? CategoryIcon
    {
        get => _categoryIcon;
        set
        {
            _categoryIcon = value;
            OnPropertyChanged();
        }
    }

    public string? CategoryColor
    {
        get => _categoryColor;
        set
        {
            _categoryColor = value;
            OnPropertyChanged();
        }
    }

    public CategorySummaryKind Kind
    {
        get => _kind;
        set
        {
            _kind = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsAppKind));
            OnPropertyChanged(nameof(IsWebKind));
            // 同步 TabSwitch 选中项
            if (KindOptions != null && _selectedKind?.Id != (int)value)
            {
                _selectedKind = KindOptions[(int)value];
                OnPropertyChanged(nameof(SelectedKind));
            }
        }
    }

    public int CategoryId
    {
        get => _categoryId;
        set
        {
            _categoryId = value;
            OnPropertyChanged();
        }
    }

    public string TotalText
    {
        get => _totalText;
        set
        {
            _totalText = value;
            OnPropertyChanged();
        }
    }

    public int ActiveDays
    {
        get => _activeDays;
        set
        {
            _activeDays = value;
            OnPropertyChanged();
        }
    }

    public string AverageDailyText
    {
        get => _averageDailyText;
        set
        {
            _averageDailyText = value;
            OnPropertyChanged();
        }
    }

    public long TotalSeconds
    {
        get => _totalSeconds;
        set
        {
            _totalSeconds = value;
            OnPropertyChanged();
        }
    }

    public long AverageDailySeconds
    {
        get => _averageDailySeconds;
        set
        {
            _averageDailySeconds = value;
            OnPropertyChanged();
        }
    }

    public double PeakSeconds
    {
        get => _peakSeconds;
        set
        {
            _peakSeconds = value;
            OnPropertyChanged();
        }
    }

    public string RangeLabel
    {
        get => _rangeLabel;
        set
        {
            _rangeLabel = value;
            OnPropertyChanged();
        }
    }

    /// <summary>环比对比周期标签</summary>
    public string? VsLabel
    {
        get => _vsLabel;
        set { _vsLabel = value; OnPropertyChanged(); }
    }

    /// <summary>上一周期总时长</summary>
    public long PreviousTotalSeconds
    {
        get => _previousTotalSeconds;
        set { _previousTotalSeconds = value; OnPropertyChanged(); }
    }

    /// <summary>环比差值文本</summary>
    public string? DiffText
    {
        get => _diffText;
        set { _diffText = value; OnPropertyChanged(); }
    }

    public bool DiffUp
    {
        get => _diffUp;
        set { _diffUp = value; OnPropertyChanged(); }
    }

    public bool DiffDown
    {
        get => _diffDown;
        set { _diffDown = value; OnPropertyChanged(); }
    }

    public bool DiffFlat
    {
        get => _diffFlat;
        set { _diffFlat = value; OnPropertyChanged(); }
    }

    /// <summary>上一周期有数据时显示环比徽章</summary>
    public bool IsDiffVisible
    {
        get => _isDiffVisible;
        set { _isDiffVisible = value; OnPropertyChanged(); }
    }

    /// <summary>单日峰值文本</summary>
    public string PeakText
    {
        get => _peakText;
        set { _peakText = value; OnPropertyChanged(); }
    }

    /// <summary>单日峰值所在日期文本，如"6/20"；无数据时为空</summary>
    public string PeakMetaText
    {
        get => _peakMetaText;
        set { _peakMetaText = value; OnPropertyChanged(); }
    }

    /// <summary>柱状图单系列数据</summary>
    public List<ChartsDataModel> ColumnChartData
    {
        get => _columnChartData;
        set { _columnChartData = value; OnPropertyChanged(); }
    }

    public int ColumnSelectedIndex
    {
        get => _columnSelectedIndex;
        set { _columnSelectedIndex = value; OnPropertyChanged(); }
    }

    public bool IsCanColumnSelect
    {
        get => _isCanColumnSelect;
        set { _isCanColumnSelect = value; OnPropertyChanged(); }
    }

    /// <summary>点击列后展开的成员列表</summary>
    public List<ChartsDataModel> SelectedMembers
    {
        get => _selectedMembers;
        set { _selectedMembers = value; OnPropertyChanged(); }
    }

    public string? SelectedColumnLabel
    {
        get => _selectedColumnLabel;
        set { _selectedColumnLabel = value; OnPropertyChanged(); }
    }

    public bool HasData
    {
        get => _hasData;
        set
        {
            _hasData = value;
            OnPropertyChanged();
        }
    }

    public override async Task OnNavigatedToAsync()
    {
        if (_navigationData.Data is not CategorySummaryNavigationContext ctx) return;

        Kind = ctx.Kind;
        CategoryId = ctx.CategoryId;
        CategoryName = ctx.CategoryName ?? string.Empty;

        // 每次进入页面重置到今日，避免上次浏览的历史日期残留
        SelectedDate = DateTime.Today;

        await BuildCategoryChipsAsync().ConfigureAwait(false);
        await LoadAsync().ConfigureAwait(false);
    }

    public override Task RefreshAsync() => LoadAsync();

    private async Task OnKindChangedAsync()
    {
        // 切换 Kind，刷新 CurrentCategories
        Kind = (CategorySummaryKind)_selectedKind.Id;

        var targetList = Kind == CategorySummaryKind.App ? _appCategoryList : _webCategoryList;
        CurrentCategories = new ObservableCollection<CategoryChip>(targetList);

        // 选中第一个分类
        if (CurrentCategories.Count > 0)
        {
            var first = CurrentCategories[0];
            await OnSwitchCategoryAsync(first);
        }
    }

    private async Task BuildCategoryChipsAsync()
    {
        try
        {
            var appCats = await _categorys.GetCategoriesAsync(LoadToken).ConfigureAwait(false);
            var webCats = await _webData.GetWebSiteCategoriesAsync(LoadToken).ConfigureAwait(false);

            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                Categories.Clear();
                _appCategoryList = [];
                _webCategoryList = [];

                foreach (var c in appCats)
                {
                    var chip = new CategoryChip(
                        Kind: CategorySummaryKind.App,
                        Id: c.ID,
                        Name: c.Name ?? string.Empty,
                        Color: c.Color,
                        IsActive: Kind == CategorySummaryKind.App && c.ID == CategoryId);
                    Categories.Add(chip);
                    _appCategoryList.Add(chip);
                }
                foreach (var c in webCats)
                {
                    var chip = new CategoryChip(
                        Kind: CategorySummaryKind.Web,
                        Id: c.ID,
                        Name: c.Name ?? string.Empty,
                        Color: c.Color,
                        IsActive: Kind == CategorySummaryKind.Web && c.ID == CategoryId);
                    Categories.Add(chip);
                    _webCategoryList.Add(chip);
                }

                // 设置当前显示的分类列表
                CurrentCategories = new ObservableCollection<CategoryChip>(
                    Kind == CategorySummaryKind.App ? _appCategoryList : _webCategoryList);

                // 同步 TabSwitch 选中项
                _selectedKind = KindOptions[(int)Kind];
                OnPropertyChanged(nameof(SelectedKind));

                // 同步当前分类的颜色
                var current = Categories.FirstOrDefault(x => x.IsActive);
                CategoryColor = current?.Color;
                if (string.IsNullOrEmpty(CategoryName) && current != null)
                {
                    CategoryName = current.Name;
                }
            });
        }
        catch (Exception)
        {
            // 不致命，chip 行为空也能用
        }
    }

    private async Task OnSwitchCategoryAsync(CategoryChip chip)
    {
        if (chip == null) return;
        if (chip.Kind == Kind && chip.Id == CategoryId) return;

        Kind = chip.Kind;
        CategoryId = chip.Id;
        CategoryName = chip.Name;
        CategoryColor = chip.Color;

        // 重置所有 chip 选中态
        for (var i = 0; i < Categories.Count; i++)
        {
            var c = Categories[i];
            var active = c.Kind == Kind && c.Id == CategoryId;
            if (c.IsActive != active)
            {
                Categories[i] = c with { IsActive = active };
            }
        }
        // 同步 CurrentCategories 选中态
        for (var i = 0; i < CurrentCategories.Count; i++)
        {
            var c = CurrentCategories[i];
            var active = c.Kind == Kind && c.Id == CategoryId;
            if (c.IsActive != active)
            {
                CurrentCategories[i] = c with { IsActive = active };
            }
        }

        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        var (start, end, label, prevStart, prevEnd, vsLabel) = ResolveRange();
        RangeLabel = label;
        VsLabel = vsLabel;
        _rangeStart = start;
        _rangeEnd = end;
        // 切换 range/kind 时重置为全选；_suppress 避免重置触发重复加载，summary 完成后显式加载全区间
        _suppressMemberLoad = true;
        ColumnSelectedIndex = -1;
        _suppressMemberLoad = false;
        SelectedMembers = new();
        SelectedColumnLabel = null;

        await ExecuteAsync(async ct =>
        {
            var summary = await _summaryData.GetSummaryAsync(Kind, CategoryId, start, end, prevStart, prevEnd, ct).ConfigureAwait(false);

            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (!string.IsNullOrEmpty(summary.CategoryName) && summary.CategoryName != "?")
                {
                    CategoryName = summary.CategoryName;
                }

                TotalSeconds = summary.TotalSeconds;
                PreviousTotalSeconds = summary.PreviousTotalSeconds;
                ActiveDays = summary.ActiveDays;
                AverageDailySeconds = summary.AverageDailySeconds;
                TotalText = Time.ToString((int)Math.Min(summary.TotalSeconds, int.MaxValue));
                AverageDailyText = Time.ToString((int)Math.Min(summary.AverageDailySeconds, int.MaxValue));

                // 整体赋新集合：Path/Data 绑的是属性本身，原地增删不触发 converter/ListChart 重算
                long peak = 0;
                DateTime peakDate = default;
                var trend = new ObservableCollection<DailyPointModel>();
                foreach (var p in summary.DailyTrend)
                {
                    trend.Add(p);
                    if (p.Seconds > peak)
                    {
                        peak = p.Seconds;
                        peakDate = p.Date;
                    }
                }
                DailyTrend = trend;
                PeakSeconds = peak;
                PeakText = peak > 0 ? Time.ToString((int)Math.Min(peak, int.MaxValue)) : string.Empty;
                // DailyTrend 恒为按天序列，峰值日即秒数最大的那天
                PeakMetaText = peak > 0 ? peakDate.ToString("M'/'d") : string.Empty;

                // 柱状图数据按 range 决定列粒度
                ColumnChartData = BuildColumnChartData(summary);

                HasData = TotalSeconds > 0;
                UpdateDiff();
            });
        });

        // 默认全选，加载整个周期成员
        await LoadMembersAsync();
    }

    /// <summary>计算环比差值与方向</summary>
    private void UpdateDiff()
    {
        if (PreviousTotalSeconds > 0)
        {
            IsDiffVisible = true;
            var diff = TotalSeconds - PreviousTotalSeconds;
            DiffUp = diff > 0;
            DiffDown = diff < 0;
            DiffFlat = !DiffUp && !DiffDown;
            var absSeconds = (int)Math.Min(Math.Abs(diff), int.MaxValue);
            var sign = diff > 0 ? "+" : (diff < 0 ? "-" : string.Empty);
            DiffText = sign + Time.ToString(absSeconds);
        }
        else
        {
            IsDiffVisible = false;
            DiffUp = DiffDown = DiffFlat = false;
            DiffText = string.Empty;
        }
    }

    private (DateTime start, DateTime end, string label, DateTime? prevStart, DateTime? prevEnd, string vsLabel) ResolveRange()
    {
        // 以 SelectedDate 为锚点的整周期：日=当天，周=所在整周，月=整月，年=整年
        // 环比取上一个等长整周期，避免未完成周期误导
        var d = SelectedDate.Date;
        DateTime start, end, prevStart, prevEnd;
        string label, vsLabel;

        switch (SelectedRange.Id)
        {
            case 0: // 今日 vs 昨日
                start = end = d;
                prevStart = prevEnd = d.AddDays(-1);
                label = ResourceStrings.Today;
                vsLabel = ResourceStrings.CategoryVsYesterday;
                break;
            case 1: // 本周 vs 上周（两边各 7 天）
                start = StartOfWeek(d);
                end = start.AddDays(6);
                prevStart = start.AddDays(-7);
                prevEnd = start.AddDays(-1);
                label = ResourceStrings.ThisWeek;
                vsLabel = ResourceStrings.CategoryVsLastWeek;
                break;
            case 2: // 本月 vs 上月（两边天数相等）
                start = new DateTime(d.Year, d.Month, 1);
                end = start.AddMonths(1).AddDays(-1);
                prevStart = start.AddMonths(-1);
                prevEnd = prevStart.AddMonths(1).AddDays(-1);
                label = ResourceStrings.ThisMonth;
                vsLabel = ResourceStrings.CategoryVsLastMonth;
                break;
            case 3: // 今年 vs 去年
                start = new DateTime(d.Year, 1, 1);
                end = new DateTime(d.Year, 12, 31);
                prevStart = start.AddYears(-1);
                prevEnd = end.AddYears(-1);
                label = ResourceStrings.ThisYear;
                vsLabel = ResourceStrings.CategoryVsLastYear;
                break;
            default:
                start = end = d;
                prevStart = prevEnd = d.AddDays(-1);
                label = ResourceStrings.Today;
                vsLabel = ResourceStrings.CategoryVsYesterday;
                break;
        }

        return (start, end, label, prevStart, prevEnd, vsLabel);
    }

    private static DateSelectType RangeIdToSelectType(int rangeId) => rangeId switch
    {
        1 => DateSelectType.Week,
        2 => DateSelectType.Month,
        3 => DateSelectType.Year,
        _ => DateSelectType.Date,
    };

    private static DateTime StartOfWeek(DateTime date)
    {
        var diff = (7 + (int)date.DayOfWeek - (int)DayOfWeek.Monday) % 7;
        return date.AddDays(-diff).Date;
    }

    private static List<SelectItemModel> BuildRangeOptions()
    {
        return new List<SelectItemModel>
        {
            new() { Id = 0, Name = ResourceStrings.Today },
            new() { Id = 1, Name = ResourceStrings.ThisWeek },
            new() { Id = 2, Name = ResourceStrings.ThisMonth },
            new() { Id = 3, Name = ResourceStrings.ThisYear },
        };
    }

    private static List<SelectItemModel> BuildKindOptions()
    {
        return new List<SelectItemModel>
        {
            new() { Id = 0, Name = ResourceStrings.App },
            new() { Id = 1, Name = ResourceStrings.Website },
        };
    }

    private void OnBack(object? _)
    {
        if (_navigationData is INavigationService nav)
        {
            nav.GoBack();
        }
    }

    /// <summary>构造柱状图数据</summary>
    private List<ChartsDataModel> BuildColumnChartData(CategorySummaryModel summary)
    {
        var rangeId = SelectedRange.Id;
        double[] values;
        string[] colNames;

        if (rangeId == 0)
        {
            // 今日：24 小时分布
            var hours = summary.HourlyDistribution;
            values = new double[24];
            colNames = new string[24];
            for (var i = 0; i < 24; i++)
            {
                values[i] = i < hours.Count ? hours[i] : 0;
                colNames[i] = i.ToString();
            }
        }
        else if (rangeId == 3)
        {
            // 今年：按月聚合 12 列
            values = new double[12];
            colNames = new string[12];
            for (var m = 0; m < 12; m++)
            {
                colNames[m] = (m + 1).ToString();
            }
            foreach (var p in summary.DailyTrend)
            {
                if (p.Date.Month >= 1 && p.Date.Month <= 12)
                    values[p.Date.Month - 1] += p.Seconds;
            }
        }
        else
        {
            // 本周/本月：按天，DailyTrend 即每天秒数
            var trend = summary.DailyTrend;
            values = new double[trend.Count];
            colNames = new string[trend.Count];
            for (var i = 0; i < trend.Count; i++)
            {
                values[i] = trend[i].Seconds;
                // 列名用日号；周视图可用星期，这里统一用 d/M 简短
                colNames[i] = trend[i].Date.ToString("M/d");
            }
        }

        return new List<ChartsDataModel>
        {
            new()
            {
                Name = CategoryName,
                Values = values,
                ColumnNames = colNames,
            },
        };
    }

    /// <summary>加载成员列表：ColumnSelectedIndex &lt; 0 为全选（整个周期），否则该列子区间。</summary>
    private async Task LoadMembersAsync()
    {
        DateTime subStart, subEnd;
        string label;

        if (ColumnSelectedIndex < 0)
        {
            // 全选：整个周期
            subStart = _rangeStart.Date;
            subEnd = _rangeEnd.Date.AddDays(1);
            label = RangeLabel;
        }
        else
        {
            (subStart, subEnd, label) = ResolveColumnRange(ColumnSelectedIndex);
        }

        SelectedColumnLabel = label;

        await ExecuteAsync(async ct =>
        {
            var members = await _summaryData.GetMembersAsync(Kind, CategoryId, subStart, subEnd, ct).ConfigureAwait(false);

            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                var list = new List<ChartsDataModel>();
                foreach (var m in members)
                {
                    list.Add(new ChartsDataModel
                    {
                        Name = m.Name,
                        Value = m.Seconds,
                        Icon = m.IconFile,
                        Tag = m.Seconds > 0 ? Time.ToString((int)Math.Min(m.Seconds, int.MaxValue)) : string.Empty,
                    });
                }
                SelectedMembers = list;
            });
        });
    }

    /// <summary>根据列索引计算子区间</summary>
    private (DateTime start, DateTime end, string label) ResolveColumnRange(int colIndex)
    {
        var rangeId = SelectedRange.Id;
        if (rangeId == 0)
        {
            // 今日 24 小时：colIndex 即小时，半开 [hour, hour+1)
            var day = _rangeStart.Date;
            var start = day.AddHours(colIndex);
            var end = day.AddHours(colIndex + 1);
            return (start, end, start.ToString("HH:00"));
        }
        if (rangeId == 3)
        {
            // 今年 12 月：colIndex 即月份，半开 [月初, 下月初)
            var year = _rangeStart.Year;
            var month = colIndex + 1;
            var start = new DateTime(year, month, 1);
            var end = start.AddMonths(1);
            return (start, end, start.ToString("yyyy/MM"));
        }
        // 本周/本月：colIndex 即天偏移（DailyTrend 顺序），半开 [day, day+1)
        var d = _rangeStart.Date.AddDays(colIndex);
        var e = d.AddDays(1);
        return (d, e, d.ToString("M/d"));
    }
}

public record CategoryChip(
    CategorySummaryKind Kind,
    int Id,
    string Name,
    string? Color,
    bool IsActive);

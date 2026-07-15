using System.Collections.Generic;
using Avalonia.Controls;
using Taix.Client.Controls.Charts.Model;
using Taix.Client.Controls.Select;
using Taix.Client.PageState;

namespace Taix.Client.Models;

[GeneratePageState]
public partial class IndexPageModel : ModelBase
{
    private List<ChartsDataModel> _appMoreData;
    private List<ChartsDataModel> _appFrequentUseData;

    private int _frequentUseNum;

    private int _moreNum;
    private SelectItemModel _moreType;

    private int _tabbarSelectedIndex;

    private List<ChartsDataModel> _webFrequentUseData;
    private List<ChartsDataModel> _webMoreData;

    private List<ChartsDataModel> _weekData;

    private SelectItemModel _selectedPeriod;
    private List<SelectItemModel> _periodOptions;

    /// <summary>
    /// 周期选项（今日/本周）
    /// </summary>
    public List<SelectItemModel> PeriodOptions
    {
        get => _periodOptions;
        set
        {
            _periodOptions = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 选中的周期
    /// </summary>
    [PageState(LookupFrom = nameof(PeriodOptions))]
    public SelectItemModel SelectedPeriod
    {
        get => _selectedPeriod;
        set
        {
            _selectedPeriod = value;
            OnPropertyChanged();
            if (value != null)
                TabbarSelectedIndex = value.Id;
        }
    }

    /// <summary>
    /// tabbar selected item index (内部使用)
    /// </summary>
    [PageState]
    public int TabbarSelectedIndex
    {
        get => _tabbarSelectedIndex;
        set
        {
            _tabbarSelectedIndex = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// week data
    /// </summary>
    [PageState(DataCache = true)]
    public List<ChartsDataModel> WeekData
    {
        get => _weekData;
        set
        {
            _weekData = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 最为频繁条数
    /// </summary>
    public int FrequentUseNum
    {
        get => _frequentUseNum;
        set
        {
            _frequentUseNum = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 更多条数
    /// </summary>
    public int MoreNum
    {
        get => _moreNum;
        set
        {
            _moreNum = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 网页最为频繁数据
    /// </summary>
    [PageState(DataCache = true)]
    public List<ChartsDataModel> WebFrequentUseData
    {
        get => _webFrequentUseData;
        set
        {
            _webFrequentUseData = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 更多展示数据类型（0=应用/1=网页）
    /// </summary>
    [PageState(LookupFrom = nameof(MoreTypeOptions))]
    public SelectItemModel MoreType
    {
        get => _moreType;
        set
        {
            _moreType = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 应用最为频繁数据
    /// </summary>
    [PageState(DataCache = true)]
    public List<ChartsDataModel> AppFrequentUseData
    {
        get => _appFrequentUseData;
        set
        {
            _appFrequentUseData = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 应用更多数据
    /// </summary>
    [PageState(DataCache = true)]
    public List<ChartsDataModel> AppMoreData
    {
        get => _appMoreData;
        set
        {
            _appMoreData = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 网页更多数据
    /// </summary>
    [PageState(DataCache = true)]
    public List<ChartsDataModel> WebMoreData
    {
        get => _webMoreData;
        set
        {
            _webMoreData = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 更多展示数据类型选项（应用/网页）
    /// </summary>
    public List<SelectItemModel> MoreTypeOptions { get; set; } = [];
}

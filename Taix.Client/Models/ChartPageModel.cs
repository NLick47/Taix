using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia.Controls;
using Taix.Client.Controls.Charts.Model;
using Taix.Client.Controls.Select;
using Taix.Client.PageState;
using Taix.Client.Shared.Models;

namespace Taix.Client.Models;

[GeneratePageState]
public partial class ChartPageModel : ModelBase
{
    private string _appCount;

    private int _columnSelectedIndex = -1;
    private List<ChartsDataModel> _data;

    private double _dataMaximum;
    private DateTime _date;
    private List<ChartsDataModel> _dayHoursData;
    private string _dayHoursSelectedTime;
    private string _diffAppCountType;
    private string _diffAppCountValue;

    private string _diffTotalTimeType;
    private string _diffTotalTimeValue;
    private bool _isCanColumnSelect = true;
    private double _lastWebPageCount;
    private double _lastWebSiteCount;

    private double _lastWebTotalTime;

    private DateTime _monthDate;

    private int _nameIndexStart;

    private List<ChartsDataModel> _radarData;

    private DateTime _weekDate;

    private ObservableCollection<string> _tabbarData;

    private int _tabbarSelectedIndex;

    private AppModel _top1App;

    private List<ChartsDataModel> _topData;

    private string _topHours;

    private string _totalHours;

    private List<ChartsDataModel> _webBrowseStatisticsData;

    private List<ChartsDataModel> _webCategoriesPieData;

    private int _webColSelectedIndex = -1;
    private double _webPageCount;
    private double _webSiteCount;

    private List<ChartsDataModel> _webSitesColSelectedData;

    private string _webSitesColSelectedTimeText;

    private List<ChartsDataModel> _webSitesTopData;
    private double _webTotalTime;

    private string _webTotalTimeText;
    private string _weekDateStr;



    private DateTime _yearDate;

    public List<SelectItemModel> PeriodOptions { get; set; } = [];

    [PageState(LookupFrom = nameof(ShowTypeOptions))]
    public new SelectItemModel ShowType
    {
        get => base.ShowType;
        set => base.ShowType = value;
    }


    [PageState(DataCache = true)]
    public List<ChartsDataModel> Data
    {
        get => _data;
        set
        {
            _data = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// tabbar data
    /// </summary>
    public ObservableCollection<string> TabbarData
    {
        get => _tabbarData;
        set
        {
            _tabbarData = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// tabbar selected item index
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

    private SelectItemModel? _selectedPeriod;
    [PageState(LookupFrom = nameof(PeriodOptions))]
    public SelectItemModel? SelectedPeriod
    {
        get => _selectedPeriod;
        set { _selectedPeriod = value; OnPropertyChanged(); }
    }

    [PageState]
    public string WeekDateStr
    {
        get => _weekDateStr;
        set
        {
            _weekDateStr = value;
            OnPropertyChanged();
        }
    }

    [PageState]
    public DateTime Date
    {
        get => _date;
        set
        {
            _date = value;
            OnPropertyChanged();
        }
    }



    [PageState]
    public DateTime WeekDate
    {
        get => _weekDate;
        set
        {
            _weekDate = value;
            OnPropertyChanged();
        }
    }

    [PageState]
    public DateTime MonthDate
    {
        get => _monthDate;
        set
        {
            _monthDate = value;
            OnPropertyChanged();
        }
    }

    [PageState]
    public DateTime YearDate
    {
        get => _yearDate;
        set
        {
            _yearDate = value;
            OnPropertyChanged();
        }
    }

    [PageState]
    public int NameIndexStart
    {
        get => _nameIndexStart;
        set
        {
            _nameIndexStart = value;
            OnPropertyChanged();
        }
    }

    [PageState(DataCache = true)]
    public List<ChartsDataModel> TopData
    {
        get => _topData;
        set
        {
            _topData = value;
            OnPropertyChanged();
        }
    }

    [PageState]
    public double DataMaximum
    {
        get => _dataMaximum;
        set
        {
            _dataMaximum = value;
            OnPropertyChanged();
        }
    }

    [PageState]
    public string TotalHours
    {
        get => _totalHours;
        set
        {
            _totalHours = value;
            OnPropertyChanged();
        }
    }

    [PageState]
    public string TopHours
    {
        get => _topHours;
        set
        {
            _topHours = value;
            OnPropertyChanged();
        }
    }

    [PageState]
    public string AppCount
    {
        get => _appCount;
        set
        {
            _appCount = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 柱状图选中列索引
    /// </summary>
    [PageState]
    public int ColumnSelectedIndex
    {
        get => _columnSelectedIndex;
        set
        {
            _columnSelectedIndex = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 指定时段app数据
    /// </summary>
    [PageState(DataCache = true)]
    public List<ChartsDataModel> DayHoursData
    {
        get => _dayHoursData;
        set
        {
            _dayHoursData = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 选中时段
    /// </summary>
    [PageState]
    public string DayHoursSelectedTime
    {
        get => _dayHoursSelectedTime;
        set
        {
            _dayHoursSelectedTime = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 是否允许选择
    /// </summary>
    [PageState]
    public bool IsCanColumnSelect
    {
        get => _isCanColumnSelect;
        set
        {
            _isCanColumnSelect = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 雷达图数据
    /// </summary>
    [PageState(DataCache = true)]
    public List<ChartsDataModel> RadarData
    {
        get => _radarData;
        set
        {
            _radarData = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 使用时间最长的应用
    /// </summary>
    [PageState]
    public AppModel Top1App
    {
        get => _top1App;
        set
        {
            _top1App = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 时长对比上一周期变化（0无变化，1增加，-1减少）
    /// </summary>
    [PageState]
    public string DiffTotalTimeType
    {
        get => _diffTotalTimeType;
        set
        {
            _diffTotalTimeType = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 应用量对比上一周期变化（0无变化，1增加，-1减少）
    /// </summary>
    [PageState]
    public string DiffAppCountType
    {
        get => _diffAppCountType;
        set
        {
            _diffAppCountType = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 时长差异值
    /// </summary>
    [PageState]
    public string DiffTotalTimeValue
    {
        get => _diffTotalTimeValue;
        set
        {
            _diffTotalTimeValue = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 应用量差异值
    /// </summary>
    [PageState]
    public string DiffAppCountValue
    {
        get => _diffAppCountValue;
        set
        {
            _diffAppCountValue = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 网页分类统计饼图数据
    /// </summary>
    [PageState(DataCache = true)]
    public List<ChartsDataModel> WebCategoriesPieData
    {
        get => _webCategoriesPieData;
        set
        {
            _webCategoriesPieData = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 网页浏览时长统计数据（柱状图）
    /// </summary>
    [PageState(DataCache = true)]
    public List<ChartsDataModel> WebBrowseStatisticsData
    {
        get => _webBrowseStatisticsData;
        set
        {
            _webBrowseStatisticsData = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 网页浏览总时长
    /// </summary>
    [PageState]
    public string WebTotalTimeText
    {
        get => _webTotalTimeText;
        set
        {
            _webTotalTimeText = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 网页浏览总时长
    /// </summary>
    [PageState]
    public double WebTotalTime
    {
        get => _webTotalTime;
        set
        {
            _webTotalTime = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 站点浏览数量
    /// </summary>
    [PageState]
    public double WebSiteCount
    {
        get => _webSiteCount;
        set
        {
            _webSiteCount = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 网页浏览数量
    /// </summary>
    [PageState]
    public double WebPageCount
    {
        get => _webPageCount;
        set
        {
            _webPageCount = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 上一个周期网页浏览总时长
    /// </summary>
    [PageState]
    public double LastWebTotalTime
    {
        get => _lastWebTotalTime;
        set
        {
            _lastWebTotalTime = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 上一个周期站点浏览数量
    /// </summary>
    [PageState]
    public double LastWebSiteCount
    {
        get => _lastWebSiteCount;
        set
        {
            _lastWebSiteCount = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 上一个周期网页浏览数量
    /// </summary>
    [PageState]
    public double LastWebPageCount
    {
        get => _lastWebPageCount;
        set
        {
            _lastWebPageCount = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 网页站点最为频繁
    /// </summary>
    [PageState(DataCache = true)]
    public List<ChartsDataModel> WebSitesTopData
    {
        get => _webSitesTopData;
        set
        {
            _webSitesTopData = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 网页浏览数据柱状图选中列索引
    /// </summary>
    [PageState]
    public int WebColSelectedIndex
    {
        get => _webColSelectedIndex;
        set
        {
            _webColSelectedIndex = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 网页浏览数据柱状图选中列数据
    /// </summary>
    [PageState(DataCache = true)]
    public List<ChartsDataModel> WebSitesColSelectedData
    {
        get => _webSitesColSelectedData;
        set
        {
            _webSitesColSelectedData = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 网页浏览数据柱状图选择列时间
    /// </summary>
    [PageState]
    public string WebSitesColSelectedTimeText
    {
        get => _webSitesColSelectedTimeText;
        set
        {
            _webSitesColSelectedTimeText = value;
            OnPropertyChanged();
        }
    }
}

    
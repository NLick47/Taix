using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia.Controls;
using Core.Models;
using UI.Controls.Charts.Model;
using UI.Controls.Select;

namespace UI.Models;

public class ChartPageModel : ModelBase
{
    private ContextMenu _appContextMenu;
    private string _appCount;

    private SelectItemModel _chartDataMode;

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
    private bool _isChartStack = true;
    private double _lastWebPageCount;
    private double _lastWebSiteCount;

    private double _lastWebTotalTime;

    private DateTime _monthDate;

    private int _nameIndexStart;

    private List<ChartsDataModel> _radarData;

    private SelectItemModel _selectedWeek;

    private ObservableCollection<string> _tabbarData;

    private int _tabbarSelectedIndex;

    private AppModel _top1App;

    private List<ChartsDataModel> _topData;

    private string _topHours;

    private string _totalHours;

    private List<ChartsDataModel> _webBrowseStatisticsData;

    //private SelectItemModel ShowType_;
    /// <summary>
    ///     展示数据类型（0=应用/1=网页）
    /// </summary>
    //public SelectItemModel ShowType { get { return ShowType_; } set { ShowType_ = value; OnPropertyChanged(); } }
    private List<ChartsDataModel> _webCategoriesPieData;

    private int _webColSelectedIndex = -1;
    private double _webPageCount;
    private ContextMenu _webSiteContextMenu;
    private double _webSiteCount;

    private List<ChartsDataModel> _webSitesColSelectedData;

    private string _webSitesColSelectedTimeText;

    private List<ChartsDataModel> _webSitesTopData;
    private double _webTotalTime;

    private string _webTotalTimeText;
    private string _weekDateStr;

    private List<SelectItemModel> _weekOptions;

    private DateTime _yearDate;
    
    private List<TrendDataPoint> _trendChartDataPoints;
    public List<TrendDataPoint> TrendChartDataPoints
    {
        get => _trendChartDataPoints;
        set
        {
            _trendChartDataPoints = value;
            OnPropertyChanged();
        }
    }

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
    ///     tabbar data
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
    ///     tabbar selected item index
    /// </summary>
    public int TabbarSelectedIndex
    {
        get => _tabbarSelectedIndex;
        set
        {
            _tabbarSelectedIndex = value;
            OnPropertyChanged();
        }
    }

    public string WeekDateStr
    {
        get => _weekDateStr;
        set
        {
            _weekDateStr = value;
            OnPropertyChanged();
        }
    }

    public DateTime Date
    {
        get => _date;
        set
        {
            _date = value;
            OnPropertyChanged();
        }
    }

    public List<SelectItemModel> WeekOptions
    {
        get => _weekOptions;
        set
        {
            _weekOptions = value;
            OnPropertyChanged();
        }
    }

    public SelectItemModel SelectedWeek
    {
        get => _selectedWeek;
        set
        {
            _selectedWeek = value;
            OnPropertyChanged();
        }
    }

    public DateTime MonthDate
    {
        get => _monthDate;
        set
        {
            _monthDate = value;
            OnPropertyChanged();
        }
    }

    public DateTime YearDate
    {
        get => _yearDate;
        set
        {
            _yearDate = value;
            OnPropertyChanged();
        }
    }

    public int NameIndexStart
    {
        get => _nameIndexStart;
        set
        {
            _nameIndexStart = value;
            OnPropertyChanged();
        }
    }

    public List<ChartsDataModel> TopData
    {
        get => _topData;
        set
        {
            _topData = value;
            OnPropertyChanged();
        }
    }

    public double DataMaximum
    {
        get => _dataMaximum;
        set
        {
            _dataMaximum = value;
            OnPropertyChanged();
        }
    }

    public ContextMenu AppContextMenu
    {
        get => _appContextMenu;
        set
        {
            _appContextMenu = value;
            OnPropertyChanged();
        }
    }

    public string TotalHours
    {
        get => _totalHours;
        set
        {
            _totalHours = value;
            OnPropertyChanged();
        }
    }

    public string TopHours
    {
        get => _topHours;
        set
        {
            _topHours = value;
            OnPropertyChanged();
        }
    }

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
    ///     柱状图选中列索引
    /// </summary>
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
    ///     指定时段app数据
    /// </summary>
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
    ///     选中时段
    /// </summary>
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
    ///     是否允许选择
    /// </summary>
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
    ///     图表数据模式（1=分类/2=汇总）
    /// </summary>
    public SelectItemModel ChartDataMode
    {
        get => _chartDataMode;
        set
        {
            _chartDataMode = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    ///     雷达图数据
    /// </summary>
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
    ///     是否以堆叠形式展示
    /// </summary>
    public bool IsChartStack
    {
        get => _isChartStack;
        set
        {
            _isChartStack = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    ///     使用时间最长的应用
    /// </summary>
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
    ///     时长对比上一周期变化（0无变化，1增加，-1减少）
    /// </summary>
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
    ///     应用量对比上一周期变化（0无变化，1增加，-1减少）
    /// </summary>
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
    ///     时长差异值
    /// </summary>
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
    ///     应用量差异值
    /// </summary>
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
    ///     网页分类统计饼图数据
    /// </summary>
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
    ///     网页浏览时长统计数据（柱状图）
    /// </summary>
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
    ///     网页浏览总时长
    /// </summary>
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
    ///     网页浏览总时长
    /// </summary>
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
    ///     站点浏览数量
    /// </summary>
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
    ///     网页浏览数量
    /// </summary>
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
    ///     上一个周期网页浏览总时长
    /// </summary>
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
    ///     上一个周期站点浏览数量
    /// </summary>
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
    ///     上一个周期网页浏览数量
    /// </summary>
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
    ///     网页站点最为频繁
    /// </summary>
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
    ///     网页浏览数据柱状图选中列索引
    /// </summary>
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
    ///     网页浏览数据柱状图选中列数据
    /// </summary>
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
    ///     网页浏览数据柱状图选择列时间
    /// </summary>
    public string WebSitesColSelectedTimeText
    {
        get => _webSitesColSelectedTimeText;
        set
        {
            _webSitesColSelectedTimeText = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    ///     网站右键菜单
    /// </summary>
    public ContextMenu WebSiteContextMenu
    {
        get => _webSiteContextMenu;
        set
        {
            _webSiteContextMenu = value;
            OnPropertyChanged();
        }
    }
}
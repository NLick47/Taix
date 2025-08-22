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
    private ContextMenu AppContextMenu_;
    private string AppCount_;

    private SelectItemModel ChartDataMode_;

    private int ColumnSelectedIndex_ = -1;
    private List<ChartsDataModel> Data_;

    private double DataMaximum_;
    private DateTime Date_;
    private List<ChartsDataModel> DayHoursData_;
    private string DayHoursSelectedTime_;
    private string DiffAppCountType_;
    private string DiffAppCountValue_;

    private string DiffTotalTimeType_;
    private string DiffTotalTimeValue_;
    private bool IsCanColumnSelect_ = true;
    private bool IsChartStack_ = true;
    private double LastWebPageCount_;
    private double LastWebSiteCount_;

    private double LastWebTotalTime_;

    private DateTime MonthDate_;

    private int NameIndexStart_;

    private List<ChartsDataModel> RadarData_;

    private SelectItemModel SelectedWeek_;

    private ObservableCollection<string> TabbarData_;

    private int TabbarSelectedIndex_;

    private AppModel Top1App_;

    private List<ChartsDataModel> TopData_;

    private string TopHours_;

    private string TotalHours_;

    private List<ChartsDataModel> WebBrowseStatisticsData_;

    //private SelectItemModel ShowType_;
    /// <summary>
    ///     展示数据类型（0=应用/1=网页）
    /// </summary>
    //public SelectItemModel ShowType { get { return ShowType_; } set { ShowType_ = value; OnPropertyChanged(); } }
    private List<ChartsDataModel> WebCategoriesPieData_;

    private int WebColSelectedIndex_ = -1;
    private double WebPageCount_;
    private ContextMenu WebSiteContextMenu_;
    private double WebSiteCount_;

    private List<ChartsDataModel> WebSitesColSelectedData_;

    private string WebSitesColSelectedTimeText_;

    private List<ChartsDataModel> WebSitesTopData_;
    private double WebTotalTime_;

    private string WebTotalTimeText_;
    private string WeekDateStr_;

    private List<SelectItemModel> WeekOptions_;

    private DateTime YearDate_;

    public List<ChartsDataModel> Data
    {
        get => Data_;
        set
        {
            Data_ = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    ///     tabbar data
    /// </summary>
    public ObservableCollection<string> TabbarData
    {
        get => TabbarData_;
        set
        {
            TabbarData_ = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    ///     tabbar selected item index
    /// </summary>
    public int TabbarSelectedIndex
    {
        get => TabbarSelectedIndex_;
        set
        {
            TabbarSelectedIndex_ = value;
            OnPropertyChanged();
        }
    }

    public string WeekDateStr
    {
        get => WeekDateStr_;
        set
        {
            WeekDateStr_ = value;
            OnPropertyChanged();
        }
    }

    public DateTime Date
    {
        get => Date_;
        set
        {
            Date_ = value;
            OnPropertyChanged();
        }
    }

    public List<SelectItemModel> WeekOptions
    {
        get => WeekOptions_;
        set
        {
            WeekOptions_ = value;
            OnPropertyChanged();
        }
    }

    public SelectItemModel SelectedWeek
    {
        get => SelectedWeek_;
        set
        {
            SelectedWeek_ = value;
            OnPropertyChanged();
        }
    }

    public DateTime MonthDate
    {
        get => MonthDate_;
        set
        {
            MonthDate_ = value;
            OnPropertyChanged();
        }
    }

    public DateTime YearDate
    {
        get => YearDate_;
        set
        {
            YearDate_ = value;
            OnPropertyChanged();
        }
    }

    public int NameIndexStart
    {
        get => NameIndexStart_;
        set
        {
            NameIndexStart_ = value;
            OnPropertyChanged();
        }
    }

    public List<ChartsDataModel> TopData
    {
        get => TopData_;
        set
        {
            TopData_ = value;
            OnPropertyChanged();
        }
    }

    public double DataMaximum
    {
        get => DataMaximum_;
        set
        {
            DataMaximum_ = value;
            OnPropertyChanged();
        }
    }

    public ContextMenu AppContextMenu
    {
        get => AppContextMenu_;
        set
        {
            AppContextMenu_ = value;
            OnPropertyChanged();
        }
    }

    public string TotalHours
    {
        get => TotalHours_;
        set
        {
            TotalHours_ = value;
            OnPropertyChanged();
        }
    }

    public string TopHours
    {
        get => TopHours_;
        set
        {
            TopHours_ = value;
            OnPropertyChanged();
        }
    }

    public string AppCount
    {
        get => AppCount_;
        set
        {
            AppCount_ = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    ///     柱状图选中列索引
    /// </summary>
    public int ColumnSelectedIndex
    {
        get => ColumnSelectedIndex_;
        set
        {
            ColumnSelectedIndex_ = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    ///     指定时段app数据
    /// </summary>
    public List<ChartsDataModel> DayHoursData
    {
        get => DayHoursData_;
        set
        {
            DayHoursData_ = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    ///     选中时段
    /// </summary>
    public string DayHoursSelectedTime
    {
        get => DayHoursSelectedTime_;
        set
        {
            DayHoursSelectedTime_ = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    ///     是否允许选择
    /// </summary>
    public bool IsCanColumnSelect
    {
        get => IsCanColumnSelect_;
        set
        {
            IsCanColumnSelect_ = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    ///     图表数据模式（1=分类/2=汇总）
    /// </summary>
    public SelectItemModel ChartDataMode
    {
        get => ChartDataMode_;
        set
        {
            ChartDataMode_ = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    ///     雷达图数据
    /// </summary>
    public List<ChartsDataModel> RadarData
    {
        get => RadarData_;
        set
        {
            RadarData_ = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    ///     是否以堆叠形式展示
    /// </summary>
    public bool IsChartStack
    {
        get => IsChartStack_;
        set
        {
            IsChartStack_ = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    ///     使用时间最长的应用
    /// </summary>
    public AppModel Top1App
    {
        get => Top1App_;
        set
        {
            Top1App_ = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    ///     时长对比上一周期变化（0无变化，1增加，-1减少）
    /// </summary>
    public string DiffTotalTimeType
    {
        get => DiffTotalTimeType_;
        set
        {
            DiffTotalTimeType_ = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    ///     应用量对比上一周期变化（0无变化，1增加，-1减少）
    /// </summary>
    public string DiffAppCountType
    {
        get => DiffAppCountType_;
        set
        {
            DiffAppCountType_ = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    ///     时长差异值
    /// </summary>
    public string DiffTotalTimeValue
    {
        get => DiffTotalTimeValue_;
        set
        {
            DiffTotalTimeValue_ = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    ///     应用量差异值
    /// </summary>
    public string DiffAppCountValue
    {
        get => DiffAppCountValue_;
        set
        {
            DiffAppCountValue_ = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    ///     网页分类统计饼图数据
    /// </summary>
    public List<ChartsDataModel> WebCategoriesPieData
    {
        get => WebCategoriesPieData_;
        set
        {
            WebCategoriesPieData_ = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    ///     网页浏览时长统计数据（柱状图）
    /// </summary>
    public List<ChartsDataModel> WebBrowseStatisticsData
    {
        get => WebBrowseStatisticsData_;
        set
        {
            WebBrowseStatisticsData_ = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    ///     网页浏览总时长
    /// </summary>
    public string WebTotalTimeText
    {
        get => WebTotalTimeText_;
        set
        {
            WebTotalTimeText_ = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    ///     网页浏览总时长
    /// </summary>
    public double WebTotalTime
    {
        get => WebTotalTime_;
        set
        {
            WebTotalTime_ = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    ///     站点浏览数量
    /// </summary>
    public double WebSiteCount
    {
        get => WebSiteCount_;
        set
        {
            WebSiteCount_ = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    ///     网页浏览数量
    /// </summary>
    public double WebPageCount
    {
        get => WebPageCount_;
        set
        {
            WebPageCount_ = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    ///     上一个周期网页浏览总时长
    /// </summary>
    public double LastWebTotalTime
    {
        get => LastWebTotalTime_;
        set
        {
            LastWebTotalTime_ = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    ///     上一个周期站点浏览数量
    /// </summary>
    public double LastWebSiteCount
    {
        get => LastWebSiteCount_;
        set
        {
            LastWebSiteCount_ = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    ///     上一个周期网页浏览数量
    /// </summary>
    public double LastWebPageCount
    {
        get => LastWebPageCount_;
        set
        {
            LastWebPageCount_ = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    ///     网页站点最为频繁
    /// </summary>
    public List<ChartsDataModel> WebSitesTopData
    {
        get => WebSitesTopData_;
        set
        {
            WebSitesTopData_ = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    ///     网页浏览数据柱状图选中列索引
    /// </summary>
    public int WebColSelectedIndex
    {
        get => WebColSelectedIndex_;
        set
        {
            WebColSelectedIndex_ = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    ///     网页浏览数据柱状图选中列数据
    /// </summary>
    public List<ChartsDataModel> WebSitesColSelectedData
    {
        get => WebSitesColSelectedData_;
        set
        {
            WebSitesColSelectedData_ = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    ///     网页浏览数据柱状图选择列时间
    /// </summary>
    public string WebSitesColSelectedTimeText
    {
        get => WebSitesColSelectedTimeText_;
        set
        {
            WebSitesColSelectedTimeText_ = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    ///     网站右键菜单
    /// </summary>
    public ContextMenu WebSiteContextMenu
    {
        get => WebSiteContextMenu_;
        set
        {
            WebSiteContextMenu_ = value;
            OnPropertyChanged();
        }
    }
}
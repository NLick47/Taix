using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia.Controls;
using Core.Models;
using UI.Controls.Charts.Model;
using UI.Controls.Select;

namespace UI.Models;

public class DetailPageModel : UINotifyPropertyChanged
{
    private AppModel? _app;
    private ContextMenu _appContextMenu;

    private bool _blockBtnVisibility;

    private bool _cancelBlockBtnVisibility = true;

    private SelectItemModel _category;

    private List<SelectItemModel> _categorys;


    //  图表数据
    private List<ChartsDataModel> _chartData;
    private DateTime _chartDate;
    private List<ChartsDataModel> _data;
    private double _dataMaximum;

    private DateTime _date;


    private bool _isIgnore;
    private bool _isLoading;
    private bool _isRegexIgnore;

    private List<AppModel> _linkApps;

    private string _longDay;

    private DateTime _monthDate;

    private int _nameIndexStart;

    private string _ratio;

    private SelectItemModel _selectedWeek;

    private ObservableCollection<string> _tabbarData;

    private int _tabbarSelectedIndex;

    private string _todayTime;

    private string _total;
    private string _weekDateStr;

    private List<SelectItemModel> _weekOptions;

    private DateTime _yearDate;

    private string _yesterday;

    /// <summary>
    ///     data
    /// </summary>
    public List<ChartsDataModel> Data
    {
        get => _data;
        set
        {
            _data = value;
            OnPropertyChanged();
        }
    }

    public AppModel? App
    {
        get => _app;
        set
        {
            _app = value;
            OnPropertyChanged();
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            _isLoading = value;
            OnPropertyChanged();
        }
    }

    public string Total
    {
        get => _total;
        set
        {
            _total = value;
            OnPropertyChanged();
        }
    }

    public string Ratio
    {
        get => _ratio;
        set
        {
            _ratio = value;
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

    public string LongDay
    {
        get => _longDay;
        set
        {
            _longDay = value;
            OnPropertyChanged();
        }
    }

    public bool IsIgnore
    {
        get => _isIgnore;
        set
        {
            _isIgnore = value;
            if (IsIgnore)
            {
                BlockBtnVisibility = false;
                CancelBlockBtnVisibility = true;
            }
            else
            {
                BlockBtnVisibility = true;
                CancelBlockBtnVisibility = false;
            }

            OnPropertyChanged();
        }
    }

    public bool BlockBtnVisibility
    {
        get => _blockBtnVisibility;
        set
        {
            _blockBtnVisibility = value;
            OnPropertyChanged();
        }
    }

    public bool CancelBlockBtnVisibility
    {
        get => _cancelBlockBtnVisibility;
        set
        {
            _cancelBlockBtnVisibility = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    ///     今日使用时长
    /// </summary>
    public string TodayTime
    {
        get => _todayTime;
        set
        {
            _todayTime = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    ///     相比昨日
    /// </summary>
    public string Yesterday
    {
        get => _yesterday;
        set
        {
            _yesterday = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    ///     分类数据
    /// </summary>
    public List<SelectItemModel> Categorys
    {
        get => _categorys;
        set
        {
            _categorys = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    ///     当前分类
    /// </summary>
    public SelectItemModel Category
    {
        get => _category;
        set
        {
            _category = value;
            OnPropertyChanged();
        }
    }

    public List<ChartsDataModel> ChartData
    {
        get => _chartData;
        set
        {
            _chartData = value;
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

    public DateTime ChartDate
    {
        get => _chartDate;
        set
        {
            _chartDate = value;
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

    public double DataMaximum
    {
        get => _dataMaximum;
        set
        {
            _dataMaximum = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    ///     关联的应用
    /// </summary>
    public List<AppModel> LinkApps
    {
        get => _linkApps;
        set
        {
            _linkApps = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    ///     是否已被正则忽略
    /// </summary>
    public bool IsRegexIgnore
    {
        get => _isRegexIgnore;
        set
        {
            _isRegexIgnore = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    ///     应用右键菜单
    /// </summary>
    public ContextMenu AppContextMenu
    {
        get => _appContextMenu;
        set
        {
            _appContextMenu = value;
            OnPropertyChanged();
        }
    }
}
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia.Controls;
using Taix.Client.Controls.Charts.Model;
using Taix.Client.Controls.Select;
using Taix.Client.Shared.Models;

namespace Taix.Client.Models;

public class DetailPageModel : ModelBase
{
    private AppModel? _app;
    private ContextMenu? _appContextMenu;
    private bool _blockBtnVisibility;
    private bool _cancelBlockBtnVisibility;
    private SelectItemModel? _category;
    private List<SelectItemModel> _categorys = [];
    private List<ChartsDataModel> _chartData = [];
    private DateTime _chartDate;
    private List<ChartsDataModel>? _data;
    private double _dataMaximum;
    private DateTime _date;
    private bool _isIgnore;
    private bool _isRegexIgnore;
    private string _longDay = string.Empty;
    private DateTime _monthDate;
    private int _nameIndexStart;
    private string _ratio = string.Empty;
    private SelectItemModel? _selectedWeek;
    private ObservableCollection<string> _tabbarData = [];
    private int _tabbarSelectedIndex;
    private string _todayTime = string.Empty;
    private string _total = string.Empty;
    private string _weekDateStr = string.Empty;
    private List<SelectItemModel> _weekOptions = [];
    private DateTime _weekDate;
    private DateTime _yearDate;
    private string _yesterday = string.Empty;
    private SelectItemModel? _selectedPeriod;
    private List<SelectItemModel> _periodOptions = [];

    public List<ChartsDataModel>? Data
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
            BlockBtnVisibility = !value;
            CancelBlockBtnVisibility = value;
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

    public string TodayTime
    {
        get => _todayTime;
        set
        {
            _todayTime = value;
            OnPropertyChanged();
        }
    }

    public string Yesterday
    {
        get => _yesterday;
        set
        {
            _yesterday = value;
            OnPropertyChanged();
        }
    }

    public List<SelectItemModel> Categorys
    {
        get => _categorys;
        set
        {
            _categorys = value;
            OnPropertyChanged();
        }
    }

    public SelectItemModel? Category
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

    public ObservableCollection<string> TabbarData
    {
        get => _tabbarData;
        set
        {
            _tabbarData = value;
            OnPropertyChanged();
        }
    }

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

    public SelectItemModel? SelectedWeek
    {
        get => _selectedWeek;
        set
        {
            _selectedWeek = value;
            OnPropertyChanged();
        }
    }

    public DateTime WeekDate
    {
        get => _weekDate;
        set
        {
            _weekDate = value;
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

    public bool IsRegexIgnore
    {
        get => _isRegexIgnore;
        set
        {
            _isRegexIgnore = value;
            OnPropertyChanged();
        }
    }

    public ContextMenu? AppContextMenu
    {
        get => _appContextMenu;
        set
        {
            _appContextMenu = value;
            OnPropertyChanged();
        }
    }

    public List<SelectItemModel> PeriodOptions
    {
        get => _periodOptions;
        set
        {
            _periodOptions = value;
            OnPropertyChanged();
        }
    }

    public SelectItemModel? SelectedPeriod
    {
        get => _selectedPeriod;
        set
        {
            _selectedPeriod = value;
            OnPropertyChanged();
        }
    }
}

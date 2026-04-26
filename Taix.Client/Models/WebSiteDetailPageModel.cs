using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia.Controls;
using Taix.Client.Controls.Charts.Model;
using Taix.Client.Controls.Select;
using Taix.Client.Shared.Models.Db;

namespace Taix.Client.Models;

public class WebSiteDetailPageModel : ModelBase
{
    private List<SelectItemModel> _categories = [];
    private SelectItemModel? _category;
    private List<ChartsDataModel> _chartData = [];
    private DateTime _chartDate;
    private bool _isIgnore;
    private DateTime _monthDate;
    private int _nameIndexStart;
    private SelectItemModel _selectedWeek = null!;
    private ObservableCollection<string> _tabbarData = [];
    private int _tabbarSelectedIndex;
    private List<WebBrowseLogModel> _webPageData = [];
    private WebBrowseLogModel? _webPageSelectedItem;
    private WebSiteModel _webSite = null!;
    private ContextMenu? _webSiteContextMenu;
    private string _weekDateStr = string.Empty;
    private List<SelectItemModel> _weekOptions = [];
    private DateTime _yearDate;

    public WebSiteModel WebSite
    {
        get => _webSite;
        set
        {
            _webSite = value;
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

    public List<WebBrowseLogModel> WebPageData
    {
        get => _webPageData;
        set
        {
            _webPageData = value;
            OnPropertyChanged();
        }
    }

    public WebBrowseLogModel? WebPageSelectedItem
    {
        get => _webPageSelectedItem;
        set
        {
            _webPageSelectedItem = value;
            OnPropertyChanged();
        }
    }

    public List<SelectItemModel> Categories
    {
        get => _categories;
        set
        {
            _categories = value;
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

    public ContextMenu? WebSiteContextMenu
    {
        get => _webSiteContextMenu;
        set
        {
            _webSiteContextMenu = value;
            OnPropertyChanged();
        }
    }

    public bool IsIgnore
    {
        get => _isIgnore;
        set
        {
            _isIgnore = value;
            OnPropertyChanged();
        }
    }
}

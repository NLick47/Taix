using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia.Controls;
using Taix.Client.Controls.Charts.Model;
using Taix.Client.Controls.Select;

namespace Taix.Client.Models;

public class IndexPageModel : ModelBase
{
    private ContextMenu _appContextMenu;

    private List<ChartsDataModel> _appMoreData;
    private List<ChartsDataModel> _appFrequentUseData;

    private int _frequentUseNum;

    private int _moreNum;
    private SelectItemModel _moreType;
    private ObservableCollection<string> _tabbarData;

    private int _tabbarSelectedIndex;

    private List<ChartsDataModel> _webFrequentUseData;
    private List<ChartsDataModel> _webMoreData;
    private ContextMenu _webSiteContextMenu;


    private List<ChartsDataModel> _weekData;

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
    public List<ChartsDataModel> WeekData
    {
        get => _weekData;
        set
        {
            _weekData = value;
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
    /// 网站右键菜单
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
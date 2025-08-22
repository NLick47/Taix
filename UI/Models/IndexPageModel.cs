using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia.Controls;
using UI.Controls.Charts.Model;
using UI.Controls.Select;

namespace UI.Models;

public class IndexPageModel : ModelBase
{
    private ContextMenu AppContextMenu_;

    private List<ChartsDataModel> AppMoreData_;

    private int FrequentUseNum_;

    private bool IsLoading_;
    private int MoreNum_;
    private SelectItemModel MoreType_;
    private ObservableCollection<string> TabbarData_;

    private int TabbarSelectedIndex_;

    private List<ChartsDataModel> WebFrequentUseData_;
    private List<ChartsDataModel> WebMoreData_;
    private ContextMenu WebSiteContextMenu_;


    private List<ChartsDataModel> WeekData_;

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

    /// <summary>
    ///     week data
    /// </summary>
    public List<ChartsDataModel> WeekData
    {
        get => WeekData_;
        set
        {
            WeekData_ = value;
            OnPropertyChanged();
        }
    }

    public bool IsLoading
    {
        get => IsLoading_;
        set
        {
            IsLoading_ = value;
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

    /// <summary>
    ///     最为频繁条数
    /// </summary>
    public int FrequentUseNum
    {
        get => FrequentUseNum_;
        set
        {
            FrequentUseNum_ = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    ///     更多条数
    /// </summary>
    public int MoreNum
    {
        get => MoreNum_;
        set
        {
            MoreNum_ = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    ///     网页最为频繁数据
    /// </summary>
    public List<ChartsDataModel> WebFrequentUseData
    {
        get => WebFrequentUseData_;
        set
        {
            WebFrequentUseData_ = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    ///     更多展示数据类型（0=应用/1=网页）
    /// </summary>
    public SelectItemModel MoreType
    {
        get => MoreType_;
        set
        {
            MoreType_ = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    ///     应用更多数据
    /// </summary>
    public List<ChartsDataModel> AppMoreData
    {
        get => AppMoreData_;
        set
        {
            AppMoreData_ = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    ///     网页更多数据
    /// </summary>
    public List<ChartsDataModel> WebMoreData
    {
        get => WebMoreData_;
        set
        {
            WebMoreData_ = value;
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
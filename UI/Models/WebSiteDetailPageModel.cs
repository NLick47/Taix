using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia.Controls;
using Core.Models.Db;
using UI.Controls.Charts.Model;
using UI.Controls.Select;

namespace UI.Models;

public class WebSiteDetailPageModel : UINotifyPropertyChanged
{
    private List<SelectItemModel> Categories_;

    private SelectItemModel Category_;

    //  图表数据
    private List<ChartsDataModel> ChartData_;
    private DateTime ChartDate_;

    public bool IsIgnore_;

    private DateTime MonthDate_;
    private int NameIndexStart_;

    private SelectItemModel SelectedWeek_;

    private ObservableCollection<string> TabbarData_;

    private int TabbarSelectedIndex_;

    private List<WebBrowseLogModel> WebPageData_;
    private WebBrowseLogModel WebPageSelectedItem_;
    public WebSiteModel WebSite_;

    private ContextMenu WebSiteContextMenu_;
    private string WeekDateStr_;

    private List<SelectItemModel> WeekOptions_;

    private DateTime YearDate_;

    /// <summary>
    ///     站点
    /// </summary>
    public WebSiteModel WebSite
    {
        get => WebSite_;
        set
        {
            WebSite_ = value;
            OnPropertyChanged();
        }
    }

    public List<ChartsDataModel> ChartData
    {
        get => ChartData_;
        set
        {
            ChartData_ = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<string> TabbarData
    {
        get => TabbarData_;
        set
        {
            TabbarData_ = value;
            OnPropertyChanged();
        }
    }

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

    public DateTime ChartDate
    {
        get => ChartDate_;
        set
        {
            ChartDate_ = value;
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

    public List<WebBrowseLogModel> WebPageData
    {
        get => WebPageData_;
        set
        {
            WebPageData_ = value;
            OnPropertyChanged();
        }
    }

    public WebBrowseLogModel WebPageSelectedItem
    {
        get => WebPageSelectedItem_;
        set
        {
            WebPageSelectedItem_ = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    ///     分类数据
    /// </summary>
    public List<SelectItemModel> Categories
    {
        get => Categories_;
        set
        {
            Categories_ = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    ///     当前分类
    /// </summary>
    public SelectItemModel Category
    {
        get => Category_;
        set
        {
            Category_ = value;
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

    /// <summary>
    ///     是否已被忽略
    /// </summary>
    public bool IsIgnore
    {
        get => IsIgnore_;
        set
        {
            IsIgnore_ = value;
            OnPropertyChanged();
        }
    }
}
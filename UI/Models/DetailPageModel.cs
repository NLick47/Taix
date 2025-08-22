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
    private AppModel App_;
    private ContextMenu AppContextMenu_;

    private bool BlockBtnVisibility_;

    private bool CancelBlockBtnVisibility_ = true;

    private SelectItemModel Category_;

    private List<SelectItemModel> Categorys_;


    //  图表数据
    private List<ChartsDataModel> ChartData_;
    private DateTime ChartDate_;
    private List<ChartsDataModel> Data_;
    private double DataMaximum_;

    private DateTime Date_;


    private bool IsIgnore_;
    private bool IsLoading_;
    private bool IsRegexIgnore_;

    private List<AppModel> LinkApps_;

    private string LongDay_;

    private DateTime MonthDate_;

    private int NameIndexStart_;

    private string Ratio_;

    private SelectItemModel SelectedWeek_;

    private ObservableCollection<string> TabbarData_;

    private int TabbarSelectedIndex_;

    private string TodayTime_;

    private string Total_;
    private string WeekDateStr_;

    private List<SelectItemModel> WeekOptions_;

    private DateTime YearDate_;

    private string Yesterday_;

    /// <summary>
    ///     data
    /// </summary>
    public List<ChartsDataModel> Data
    {
        get => Data_;
        set
        {
            Data_ = value;
            OnPropertyChanged();
        }
    }

    public AppModel App
    {
        get => App_;
        set
        {
            App_ = value;
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

    public string Total
    {
        get => Total_;
        set
        {
            Total_ = value;
            OnPropertyChanged();
        }
    }

    public string Ratio
    {
        get => Ratio_;
        set
        {
            Ratio_ = value;
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

    public string LongDay
    {
        get => LongDay_;
        set
        {
            LongDay_ = value;
            OnPropertyChanged();
        }
    }

    public bool IsIgnore
    {
        get => IsIgnore_;
        set
        {
            IsIgnore_ = value;
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
        get => BlockBtnVisibility_;
        set
        {
            BlockBtnVisibility_ = value;
            OnPropertyChanged();
        }
    }

    public bool CancelBlockBtnVisibility
    {
        get => CancelBlockBtnVisibility_;
        set
        {
            CancelBlockBtnVisibility_ = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    ///     今日使用时长
    /// </summary>
    public string TodayTime
    {
        get => TodayTime_;
        set
        {
            TodayTime_ = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    ///     相比昨日
    /// </summary>
    public string Yesterday
    {
        get => Yesterday_;
        set
        {
            Yesterday_ = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    ///     分类数据
    /// </summary>
    public List<SelectItemModel> Categorys
    {
        get => Categorys_;
        set
        {
            Categorys_ = value;
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

    public List<ChartsDataModel> ChartData
    {
        get => ChartData_;
        set
        {
            ChartData_ = value;
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

    public double DataMaximum
    {
        get => DataMaximum_;
        set
        {
            DataMaximum_ = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    ///     关联的应用
    /// </summary>
    public List<AppModel> LinkApps
    {
        get => LinkApps_;
        set
        {
            LinkApps_ = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    ///     是否已被正则忽略
    /// </summary>
    public bool IsRegexIgnore
    {
        get => IsRegexIgnore_;
        set
        {
            IsRegexIgnore_ = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    ///     应用右键菜单
    /// </summary>
    public ContextMenu AppContextMenu
    {
        get => AppContextMenu_;
        set
        {
            AppContextMenu_ = value;
            OnPropertyChanged();
        }
    }
}
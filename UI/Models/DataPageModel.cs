using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia.Controls;
using UI.Controls.Charts.Model;

namespace UI.Models;

public class DataPageModel : ModelBase
{
    private ContextMenu AppContextMenu_;

    private List<ChartsDataModel> Data_;

    private DateTime DayDate_;

    private List<ChartsDataModel> MonthData_;

    private DateTime MonthDate_;
    private ObservableCollection<string> TabbarData_;

    private int TabbarSelectedIndex_;

    private List<ChartsDataModel> YearData_;

    private DateTime YearDate_;

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

    /// <summary>
    ///     MonthData
    /// </summary>
    public List<ChartsDataModel> MonthData
    {
        get => MonthData_;
        set
        {
            MonthData_ = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    ///     YearData
    /// </summary>
    public List<ChartsDataModel> YearData
    {
        get => YearData_;
        set
        {
            YearData_ = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    ///     date
    /// </summary>
    public DateTime DayDate
    {
        get => DayDate_;
        set
        {
            DayDate_ = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    ///     date
    /// </summary>
    public DateTime MonthDate
    {
        get => MonthDate_;
        set
        {
            MonthDate_ = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    ///     date
    /// </summary>
    public DateTime YearDate
    {
        get => YearDate_;
        set
        {
            YearDate_ = value;
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
}
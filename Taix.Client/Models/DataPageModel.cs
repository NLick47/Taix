using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia.Controls;
using Taix.Client.Controls.Charts.Model;

namespace Taix.Client.Models;

public class DataPageModel : ModelBase
{
    private ContextMenu _appContextMenu;

    private List<ChartsDataModel> _data;

    private DateTime _dayDate;

    private List<ChartsDataModel> _monthData;

    private DateTime _monthDate;
    private ObservableCollection<string> _tabbarData;

    private int _tabbarSelectedIndex;

    private List<ChartsDataModel> _yearData;

    private DateTime _yearDate;

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

    /// <summary>
    ///     MonthData
    /// </summary>
    public List<ChartsDataModel> MonthData
    {
        get => _monthData;
        set
        {
            _monthData = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    ///     YearData
    /// </summary>
    public List<ChartsDataModel> YearData
    {
        get => _yearData;
        set
        {
            _yearData = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    ///     date
    /// </summary>
    public DateTime DayDate
    {
        get => _dayDate;
        set
        {
            _dayDate = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    ///     date
    /// </summary>
    public DateTime MonthDate
    {
        get => _monthDate;
        set
        {
            _monthDate = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    ///     date
    /// </summary>
    public DateTime YearDate
    {
        get => _yearDate;
        set
        {
            _yearDate = value;
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
}
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using Taix.Client.Controls.Charts.Model;
using Taix.Client.Controls.Select;
using Taix.Client.Controls.Timeline;

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
    private SelectItemModel? _selectedPeriod;

    private List<ChartsDataModel> _yearData;
    private DateTime _yearDate;
    private List<ChartsDataModel> _weekData;
    private DateTime _weekDate;

    #region 时间轴

    private List<TimelineUsageItem> _timelineUsageItems = [];
    public List<TimelineUsageItem> TimelineUsageItems
    {
        get => _timelineUsageItems;
        set { _timelineUsageItems = value; OnPropertyChanged(); }
    }

    private List<MultiTrackTimelineItem> _multiTrackItems = [];
    public List<MultiTrackTimelineItem> MultiTrackItems
    {
        get => _multiTrackItems;
        set { _multiTrackItems = value; OnPropertyChanged(); }
    }

    private double _timelineStartHour;
    public double TimelineStartHour
    {
        get => _timelineStartHour;
        set
        {
            _timelineStartHour = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(TimelineRangeText));
        }
    }

    private double _timelineEndHour = 24.0;
    public double TimelineEndHour
    {
        get => _timelineEndHour;
        set
        {
            _timelineEndHour = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(TimelineRangeText));
        }
    }

    private bool _useCategoryColor;
    public bool UseCategoryColor
    {
        get => _useCategoryColor;
        set { _useCategoryColor = value; OnPropertyChanged(); }
    }

    private SelectItemModel _selectedColorMode;
    public SelectItemModel SelectedColorMode
    {
        get => _selectedColorMode;
        set
        {
            _selectedColorMode = value;
            UseCategoryColor = value?.Id == 1;
            OnPropertyChanged();
        }
    }

    public List<SelectItemModel> ColorModeOptions { get; } =
    [
        new() { Id = 0, Name = ResourceStrings.TimelineAppColor },
        new() { Id = 1, Name = ResourceStrings.TimelineCategoryColor }
    ];

    private string _multiTrackTotalDurationText = "";
    public string MultiTrackTotalDurationText
    {
        get => _multiTrackTotalDurationText;
        set { _multiTrackTotalDurationText = value; OnPropertyChanged(); }
    }

    public string TimelineRangeText
    {
        get
        {
            var start = Math.Min(_timelineStartHour, _timelineEndHour);
            var end = Math.Max(_timelineStartHour, _timelineEndHour);
            var startTime = DateTime.Today.AddHours(start);
            var endTime = DateTime.Today.AddHours(end);
            var startStr = startTime.ToString("HH:mm");
            var endStr = endTime.ToString("HH:mm");
            // 24:00 不显示为 00:00
            if (Math.Abs(end - 24.0) < 0.001 && endStr == "00:00")
                endStr = "24:00";
            return $"{startStr} ~ {endStr}";
        }
    }

    #endregion

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

    public SelectItemModel? SelectedPeriod
    {
        get => _selectedPeriod;
        set
        {
            _selectedPeriod = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// data
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
    /// MonthData
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
    /// YearData
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

    public List<ChartsDataModel> WeekData
    {
        get => _weekData;
        set
        {
            _weekData = value;
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

    /// <summary>
    /// date
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
    /// date
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
    /// date
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
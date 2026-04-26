using System;
using System.Collections.ObjectModel;

namespace Taix.Client.Models;

public class SettingPageModel : ModelBase
{
    private bool _checkUpdateBtnVisibility = true;
    private object? _data;
    private DateTime _delDataEndMonthDate;
    private DateTime _delDataStartMonthDate;
    private DateTime _exportDataEndMonthDate;
    private DateTime _exportDataStartMonthDate;
    private ObservableCollection<string> _tabbarData = [];
    private int _tabbarSelectedIndex;
    private string _version = string.Empty;

    public object? Data
    {
        get => _data;
        set
        {
            _data = value;
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

    public string Version
    {
        get => _version;
        set
        {
            _version = value;
            OnPropertyChanged();
        }
    }

    public bool CheckUpdateBtnVisibility
    {
        get => _checkUpdateBtnVisibility;
        set
        {
            _checkUpdateBtnVisibility = value;
            OnPropertyChanged();
        }
    }

    public DateTime DelDataStartMonthDate
    {
        get => _delDataStartMonthDate;
        set
        {
            _delDataStartMonthDate = value;
            OnPropertyChanged();
        }
    }

    public DateTime DelDataEndMonthDate
    {
        get => _delDataEndMonthDate;
        set
        {
            _delDataEndMonthDate = value;
            OnPropertyChanged();
        }
    }

    public DateTime ExportDataStartMonthDate
    {
        get => _exportDataStartMonthDate;
        set
        {
            _exportDataStartMonthDate = value;
            OnPropertyChanged();
        }
    }

    public DateTime ExportDataEndMonthDate
    {
        get => _exportDataEndMonthDate;
        set
        {
            _exportDataEndMonthDate = value;
            OnPropertyChanged();
        }
    }
}

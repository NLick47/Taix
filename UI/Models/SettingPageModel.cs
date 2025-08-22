using System;
using System.Collections.ObjectModel;

namespace UI.Models;

public class SettingPageModel : UINotifyPropertyChanged
{
    private bool CheckUpdateBtnVisibility_ = true;
    private object data;

    private DateTime DelDataEndMonthDate_;


    private DateTime DelDataStartMonthDate_;

    private DateTime ExportDataEndMonthDate_;

    private DateTime ExportDataStartMonthDate_;

    private ObservableCollection<string> TabbarData_;

    private int TabbarSelectedIndex_;

    private string version;

    public object Data
    {
        get => data;
        set
        {
            data = value;
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

    /// <summary>
    ///     软件版本号
    /// </summary>
    public string Version
    {
        get => version;
        set
        {
            version = value;
            OnPropertyChanged();
        }
    }

    public bool CheckUpdateBtnVisibility
    {
        get => CheckUpdateBtnVisibility_;
        set
        {
            CheckUpdateBtnVisibility_ = value;
            OnPropertyChanged();
        }
    }

    public DateTime DelDataStartMonthDate
    {
        get => DelDataStartMonthDate_;
        set
        {
            DelDataStartMonthDate_ = value;
            OnPropertyChanged();
        }
    }

    public DateTime DelDataEndMonthDate
    {
        get => DelDataEndMonthDate_;
        set
        {
            DelDataEndMonthDate_ = value;
            OnPropertyChanged();
        }
    }

    public DateTime ExportDataStartMonthDate
    {
        get => ExportDataStartMonthDate_;
        set
        {
            ExportDataStartMonthDate_ = value;
            OnPropertyChanged();
        }
    }

    public DateTime ExportDataEndMonthDate
    {
        get => ExportDataEndMonthDate_;
        set
        {
            ExportDataEndMonthDate_ = value;
            OnPropertyChanged();
        }
    }
}
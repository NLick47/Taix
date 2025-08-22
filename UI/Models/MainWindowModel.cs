using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UI.Controls;
using UI.Controls.Base;
using UI.Controls.Navigation.Models;
using UI.Controls.Window;

namespace UI.Models;

public class MainWindowModel : UINotifyPropertyChanged
{
    private ObservableCollection<NavigationItemModel> _items;

    private object Data_;

    private List<string> IndexUriList_;

    private bool IsShowNavigation_ = true;

    private bool IsShowTitleBar_;

    private bool IsShowToast_;

    private double NavigationWidth_ = 220;


    private NavigationItemModel NavSelectedItem_;

    private PageContainer PageContainer_;
    private IServiceProvider ServiceProvider_;

    private string Title_;


    private string ToastContent_;
    private IconTypes ToastIcon_;

    private ToastType ToastType_;

    private string Uri_;

    public IServiceProvider ServiceProvider
    {
        get => ServiceProvider_;
        set
        {
            ServiceProvider_ = value;
            OnPropertyChanged();
        }
    }

    public string Uri
    {
        get => Uri_;
        set
        {
            Uri_ = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    ///     页面传递数据
    /// </summary>
    public object Data
    {
        get => Data_;
        set
        {
            Data_ = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<NavigationItemModel> Items
    {
        get => _items;
        set
        {
            if (_items != value)
            {
                _items = value;
                OnPropertyChanged();
            }
        }
    }

    public double NavigationWidth
    {
        get => NavigationWidth_;
        set
        {
            NavigationWidth_ = value;
            OnPropertyChanged();
        }
    }

    public bool IsShowNavigation
    {
        get => IsShowNavigation_;
        set
        {
            IsShowNavigation_ = value;
            OnPropertyChanged();
        }
    }

    public string Title
    {
        get => Title_;
        set
        {
            Title_ = value;
            OnPropertyChanged();
        }
    }

    public bool IsShowTitleBar
    {
        get => IsShowTitleBar_;
        set
        {
            IsShowTitleBar_ = value;
            OnPropertyChanged();
        }
    }

    public NavigationItemModel NavSelectedItem
    {
        get => NavSelectedItem_;
        set
        {
            NavSelectedItem_ = value;
            OnPropertyChanged();
        }
    }

    public PageContainer PageContainer
    {
        get => PageContainer_;
        set
        {
            PageContainer_ = value;
            OnPropertyChanged();
        }
    }

    public List<string> IndexUriList
    {
        get => IndexUriList_;
        set
        {
            IndexUriList_ = value;
            OnPropertyChanged();
        }
    }

    public string ToastContent
    {
        get => ToastContent_;
        set
        {
            ToastContent_ = value;
            OnPropertyChanged();
        }
    }

    public bool IsShowToast
    {
        get => IsShowToast_;
        set
        {
            IsShowToast_ = value;
            OnPropertyChanged();
        }
    }

    public IconTypes ToastIcon
    {
        get => ToastIcon_;
        set
        {
            ToastIcon_ = value;
            OnPropertyChanged();
        }
    }

    public ToastType ToastType
    {
        get => ToastType_;
        set
        {
            ToastType_ = value;
            OnPropertyChanged();
        }
    }
}
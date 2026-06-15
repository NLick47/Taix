using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Taix.Client.Controls;
using Taix.Client.Controls.Base;
using Taix.Client.Controls.Navigation.Models;
using Taix.Client.Controls.Window;

namespace Taix.Client.Models;

public class MainWindowModel : UINotifyPropertyChanged
{
    private ObservableCollection<NavigationItemModel> _items;

    private object _data;

    private List<string> _indexUriList;

    private bool _isShowNavigation = true;

    private bool _isShowTitleBar;

    private bool _isShowToast;

    private double _navigationWidth = 220;


    private NavigationItemModel _navSelectedItem;

    private PageContainer _pageContainer;
    private IServiceProvider _serviceProvider;

    private string _title;


    private string _toastContent;
    private IconTypes _toastIcon;

    private ToastType _toastType;

    private string _uri;

    private ConnectionStatus _connectionStatus = ConnectionStatus.Checking;

    public IServiceProvider ServiceProvider
    {
        get => _serviceProvider;
        set
        {
            _serviceProvider = value;
            OnPropertyChanged();
        }
    }

    public string Uri
    {
        get => _uri;
        set
        {
            _uri = value;
            OnPropertyChanged();
        }
    }

    public ConnectionStatus ConnectionStatus
    {
        get => _connectionStatus;
        set
        {
            _connectionStatus = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 页面传递数据
    /// </summary>
    public object Data
    {
        get => _data;
        set
        {
            _data = value;
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
        get => _navigationWidth;
        set
        {
            _navigationWidth = value;
            OnPropertyChanged();
        }
    }

    public bool IsShowNavigation
    {
        get => _isShowNavigation;
        set
        {
            _isShowNavigation = value;
            OnPropertyChanged();
        }
    }

    public string Title
    {
        get => _title;
        set
        {
            _title = value;
            OnPropertyChanged();
        }
    }

    public bool IsShowTitleBar
    {
        get => _isShowTitleBar;
        set
        {
            _isShowTitleBar = value;
            OnPropertyChanged();
        }
    }

    public NavigationItemModel NavSelectedItem
    {
        get => _navSelectedItem;
        set
        {
            _navSelectedItem = value;
            OnPropertyChanged();
        }
    }

    public PageContainer PageContainer
    {
        get => _pageContainer;
        set
        {
            _pageContainer = value;
            OnPropertyChanged();
        }
    }

    public List<string> IndexUriList
    {
        get => _indexUriList;
        set
        {
            _indexUriList = value;
            OnPropertyChanged();
        }
    }

    public string ToastContent
    {
        get => _toastContent;
        set
        {
            _toastContent = value;
            OnPropertyChanged();
        }
    }

    public bool IsShowToast
    {
        get => _isShowToast;
        set
        {
            _isShowToast = value;
            OnPropertyChanged();
        }
    }

    public IconTypes ToastIcon
    {
        get => _toastIcon;
        set
        {
            _toastIcon = value;
            OnPropertyChanged();
        }
    }

    public ToastType ToastType
    {
        get => _toastType;
        set
        {
            _toastType = value;
            OnPropertyChanged();
        }
    }
}
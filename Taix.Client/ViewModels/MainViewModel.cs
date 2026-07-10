using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using ReactiveUI;
using Taix.Client.Controls.Base;
using Taix.Client.Controls.Navigation.Models;
using Taix.Client.Controls.Window;
using Taix.Client.Models;
using Taix.Client.Servicers.Interfaces;
using Taix.Client.Servicers.Updater;
using Taix.Client.Shared.Servicers.Interfaces;
using Taix.Client.Librarys.Api;
using Taix.Client.Views;
using Taix.Client.Logging;

namespace Taix.Client.ViewModels;

public class MainViewModel : MainWindowModel, IToastService, INavigationService, INavigationDataService
{
    private readonly IAppConfig _appConfig;
    private readonly IServiceProvider _serviceProvider;
    private readonly ITaixApiClient _apiClient;
    private readonly string[] _pages = [nameof(IndexPage), nameof(ChartPage), nameof(DataPage), nameof(CategoryPage)];
    private readonly CompositeDisposable _disposables = new();
    private readonly CancellationTokenSource _connectionCts = new();
    private bool _isConnectionMonitoringStarted;
    private bool _isNavigatingBack;

    private ISearchService? _searchService;
    private SearchPaletteViewModel? _searchVm;
    private bool _isSearchOpen;

    public MainViewModel(
        IServiceProvider serviceProvider,
        IAppConfig appConfig,
        ITaixApiClient apiClient
    )
    {
        _serviceProvider = serviceProvider;
        _appConfig = appConfig;
        _apiClient = apiClient;
        ServiceProvider = serviceProvider;

        OnSelectedCommand = ReactiveCommand.Create<object>(OnSelectedCommandHandle);
        GotoPageCommand = ReactiveCommand.Create<object>(OnGotoPageCommand);

#if !DEBUG
        Title = "Taix";
#else
        Title = "Taix -Debug";
#endif

        this.WhenAnyValue(x => x.Uri)
            .Where(uri => uri == nameof(IndexPage))
            .Subscribe(_ => Data = null)
            .DisposeWith(_disposables);

        InitNavigation();
    }

    private void StartConnectionMonitoring()
    {
        if (_isConnectionMonitoringStarted)
            return;
        _isConnectionMonitoringStarted = true;

        _ = Task.Run(async () =>
        {
            while (!_connectionCts.IsCancellationRequested)
            {
                await CheckConnectionAsync();
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), _connectionCts.Token);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        });
    }

    private async Task CheckConnectionAsync()
    {
        try
        {
            var isConnected = await _apiClient.HealthCheckAsync();
            var newStatus = isConnected ? ConnectionStatus.Connected : ConnectionStatus.Disconnected;

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (ConnectionStatus != newStatus)
                {
                    ConnectionStatus = newStatus;
                    if (!isConnected)
                    {
                        Error(ResourceStrings.ServerDisconnected);
                    }
                    else
                    {
                        try
                        {
                            _ = _appConfig.LoadAsync();
                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"重连后同步配置失败: {ex.Message}", ex);
                        }
                    }
                }
            });
        }
        catch
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (ConnectionStatus != ConnectionStatus.Disconnected)
                {
                    ConnectionStatus = ConnectionStatus.Disconnected;
                    Error(ResourceStrings.ServerDisconnected);
                }
            });
        }
    }

    public ReactiveCommand<object, Unit> OnSelectedCommand { get; }
    public ICommand GotoPageCommand { get; }

    private void OnGotoPageCommand(object obj)
    {
        Uri = obj.ToString();
    }

    private void InitNavigation()
    {
        IndexUriList = new List<string>(_pages);

        Items =
        [
            new NavigationItemModel
            {
                UnSelectedIcon = IconTypes.Home,
                SelectedIcon = IconTypes.HomeSolid,
                Uri = nameof(IndexPage),
                ID = -1,
            },
            new NavigationItemModel
            {
                UnSelectedIcon = IconTypes.ZeroBars,
                SelectedIcon = IconTypes.ZeroBars,
                Uri = nameof(ChartPage),
                ID = 1,
            },
            new NavigationItemModel
            {
                UnSelectedIcon = IconTypes.BulletedList,
                SelectedIcon = IconTypes.BulletedList,
                Uri = nameof(DataPage),
                ID = 2,
            },
            new NavigationItemModel
            {
                UnSelectedIcon = IconTypes.Folder,
                SelectedIcon = IconTypes.FolderFill,
                Uri = nameof(CategoryPage),
                ID = 3,
            }
        ];
    }


    public void Toast(string content, ToastType type = ToastType.Info, IconTypes icon = IconTypes.Accept)
    {
        ToastContent = content;
        ToastIcon = icon;
        ToastType = type;
        IsShowToast = true;
    }

    public void Error(string message)
    {
        Toast(message, ToastType.Error, IconTypes.Error);
    }

    public void Info(string message)
    {
        Toast(message, ToastType.Info, IconTypes.Info);
    }

    public void Success(string message)
    {
        Toast(message, ToastType.Success);
    }

    public void NavigateTo(string pageName, object? data = null)
    {
        System.Diagnostics.Debug.WriteLine($"[MainViewModel] NavigateTo: {pageName}, setting IsNavigatingBack = false");
        _isNavigatingBack = false; // 新导航重置返回状态
        Data = data;
        Uri = pageName;
    }

    public void GoBack()
    {
        System.Diagnostics.Debug.WriteLine($"[MainViewModel] GoBack called, setting IsNavigatingBack = true");
        _isNavigatingBack = true; // 设置返回导航标记
        PageContainer?.Back();
    }

    /// <summary>
    /// 当前是否为返回导航
    /// </summary>
    public bool IsNavigatingBack => _isNavigatingBack;

    /// <summary>
    /// 重置导航状态
    /// </summary>
    public void ResetNavigationState()
    {
        _isNavigatingBack = false;
    }

    /// <summary>
    /// 刷新当前页面数据
    /// </summary>
    public void RefreshCurrentPage()
    {
        if (PageContainer?.CurrentViewModel != null)
            _ = PageContainer.CurrentViewModel.RefreshAsync();
    }

    private bool _isStartupInitCompleted;

    public void LoadDefaultPage()
    {
        LoadDefaultPageInternal();

        // 订阅全局搜索 Toggle 请求。延迟到这里订阅以规避 MainViewModel↔SearchService 的构造期循环依赖
        // （SearchService 注入 INavigationService=本实例）。此时单例均已构造完毕。
        if (_searchService == null)
        {
            _searchService = _serviceProvider.GetService(typeof(ISearchService)) as ISearchService;
            if (_searchService != null)
            {
                _searchService.SearchToggleRequested += OnSearchToggle;
            }
        }

        if (_isStartupInitCompleted) return;
        _isStartupInitCompleted = true;

        var config = _appConfig.GetConfig();
        if (config.General.IsAutoUpdate)
        {
            var updateService = ServiceLocator.GetService<UpdateCheckerService>();
            if (updateService != null)
            {
                _ = Task.Run(async () =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(5));
                    await updateService.AutoCheckForUpdatesAsync();
                });
            }
        }

        StartConnectionMonitoring();
    }

    private void LoadDefaultPageInternal()
    {
        var config = _appConfig.GetConfig();
        var startPageIndex = config.General.StartPage;
        if (startPageIndex >= 0 && startPageIndex < Items.Count)
        {
            NavSelectedItem = Items[startPageIndex];
            Uri = Items[startPageIndex].Uri;
        }
    }

    private void OnSelectedCommandHandle(object obj)
    {
        if (!string.IsNullOrEmpty(NavSelectedItem?.Uri))
            Uri = NavSelectedItem.Uri;
    }

    public void Dispose()
    {
        if (_searchService != null)
        {
            _searchService.SearchToggleRequested -= OnSearchToggle;
            _searchService = null;
        }
        _searchVm?.Dispose();
        _searchVm = null;

        _connectionCts.Cancel();
        _connectionCts.Dispose();
        _disposables.Dispose();
    }

    public bool IsSearchOpen
    {
        get => _isSearchOpen;
        set
        {
            if (_isSearchOpen == value) return;
            _isSearchOpen = value;
            OnPropertyChanged();

            // 展开时确保搜索 VM 就绪，并把它的关闭回调接到本状态
            if (value)
            {
                EnsureSearchVm();
                // 每次展开重新加载，确保拿到分类增删改后的最新语料
                _ = _searchVm?.RefreshAsync();
            }
        }
    }

    public SearchPaletteViewModel SearchVm
    {
        get
        {
            EnsureSearchVm();
            return _searchVm!;
        }
    }

    private void EnsureSearchVm()
    {
        if (_searchVm != null) return;
        _searchVm = _serviceProvider.GetService(typeof(SearchPaletteViewModel)) as SearchPaletteViewModel;
        if (_searchVm != null)
        {
            // 导航跳转/清除后统一收口到 IsSearchOpen，无需独立窗口的关闭逻辑
            _searchVm.CloseRequested = () => IsSearchOpen = false;
        }
    }

    private void OnSearchToggle()
    {
        IsSearchOpen = !IsSearchOpen;
    }
}

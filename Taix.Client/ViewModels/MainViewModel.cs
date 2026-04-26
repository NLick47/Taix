using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
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
    private readonly string[] _pages = [nameof(IndexPage), nameof(DataPage), nameof(ChartPage), nameof(CategoryPage)];
    private readonly CompositeDisposable _disposables = new();
    private readonly CancellationTokenSource _connectionCts = new();
    private bool _isConnectionMonitoringStarted;

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
        LoadDefaultPageCommand = ReactiveCommand.CreateFromTask(LoadDefaultPageAsync);

        Items = new ObservableCollection<NavigationItemModel>();

#if !DEBUG
        Title = "Taix";
#else
        Title = "Taix -Debug";
#endif

        this.WhenAnyValue(x => x.Uri)
            .Where(uri => uri == nameof(IndexPageViewModel))
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
            if (ConnectionStatus != newStatus)
            {
                ConnectionStatus = newStatus;
                if (!isConnected)
                {
                    Error(ResourceStrings.ServerDisconnected);
                }
                else
                {
                    // 重连成功，同步服务端最新配置
                    try
                    {
                        await _appConfig.LoadAsync();
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"重连后同步配置失败: {ex.Message}", ex);
                    }
                }
            }
        }
        catch
        {
            if (ConnectionStatus != ConnectionStatus.Disconnected)
            {
                ConnectionStatus = ConnectionStatus.Disconnected;
                Error(ResourceStrings.ServerDisconnected);
            }
        }
    }

    public ReactiveCommand<object, Unit> OnSelectedCommand { get; }
    public ICommand GotoPageCommand { get; }
    public ReactiveCommand<Unit, Unit> LoadDefaultPageCommand { get; }

    private void OnGotoPageCommand(object obj)
    {
        Uri = obj.ToString();
    }

    private void InitNavigation()
    {
        IndexUriList = new List<string>(_pages);

        var overviewObservable = Application.Current.Resources.GetResourceObservable("SideOverview").DistinctUntilChanged();
        var statisticsObservable = Application.Current.Resources.GetResourceObservable("SideStatistics").DistinctUntilChanged();
        var detailsObservable = Application.Current.Resources.GetResourceObservable("SideDetails").DistinctUntilChanged();
        var sortObservable = Application.Current.Resources.GetResourceObservable("SideSort").DistinctUntilChanged();

        Items =
        [
            new NavigationItemModel
            {
                UnSelectedIcon = IconTypes.Home,
                SelectedIcon = IconTypes.HomeSolid,
                Uri = nameof(IndexPage),
                ID = -1
            },
            new NavigationItemModel
            {
                UnSelectedIcon = IconTypes.ZeroBars,
                SelectedIcon = IconTypes.FourBars,
                Uri = nameof(ChartPage),
                ID = 1
            },
            new NavigationItemModel
            {
                UnSelectedIcon = IconTypes.Calendar,
                SelectedIcon = IconTypes.CalendarSolid,
                Uri = nameof(DataPage),
                ID = 2
            },
            new NavigationItemModel
            {
                UnSelectedIcon = IconTypes.EndPoint,
                SelectedIcon = IconTypes.EndPointSolid,
                Uri = nameof(CategoryPage),
                ID = 3
            }
        ];

        SubscribeToResourceObservable(overviewObservable, -1);
        SubscribeToResourceObservable(statisticsObservable, 1);
        SubscribeToResourceObservable(detailsObservable, 2);
        SubscribeToResourceObservable(sortObservable, 3);
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
        Data = data;
        Uri = pageName;
    }

    public void GoBack()
    {
        PageContainer?.Back();
    }

    private async Task LoadDefaultPageAsync()
    {
        var config = _appConfig.GetConfig();
        var startPageIndex = config.General.StartPage;

        if (startPageIndex >= 0 && startPageIndex < Items.Count)
        {
            NavSelectedItem = Items[startPageIndex];
            Uri = Items[startPageIndex].Uri;
        }

        if (config.General.IsAutoUpdate)
        {
            var updateService = _serviceProvider.GetService<UpdateCheckerService>();
            if (updateService != null)
                await updateService.AutoCheckForUpdatesAsync();
        }

        StartConnectionMonitoring();
    }

    private void SubscribeToResourceObservable(IObservable<object> observable, int id)
    {
        observable.Subscribe(newTitle =>
        {
            var nv = Items.First(x => x.ID == id);
            Items.Remove(nv);
            nv.Title = newTitle as string;
            Items.Add(nv);
        });
    }

    private void OnSelectedCommandHandle(object obj)
    {
        if (!string.IsNullOrEmpty(NavSelectedItem?.Uri))
            Uri = NavSelectedItem.Uri;
    }

    public void Dispose()
    {
        _connectionCts.Cancel();
        _disposables.Dispose();
    }
}

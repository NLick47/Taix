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
        Data = data;
        Uri = pageName;
    }

    public void GoBack()
    {
        PageContainer?.Back();
    }

    private bool _isStartupInitCompleted;

    public void LoadDefaultPage()
    {
        LoadDefaultPageInternal();

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
        _connectionCts.Cancel();
        _connectionCts.Dispose();
        _disposables.Dispose();
    }
}

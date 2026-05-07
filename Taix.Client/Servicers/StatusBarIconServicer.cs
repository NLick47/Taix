using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using Taix.Client.Logging;
using Taix.Client.Shared.Servicers.Interfaces;
using Taix.Client.ViewModels;
using Taix.Client.Views;

namespace Taix.Client.Servicers;

public class StatusBarIconServicer : IStatusBarIconServicer, IDisposable
{
    public enum IconType
    {
        Normal
    }

    private TrayIcon? _trayIcon;
    private readonly IAppConfig _appConfig;
    private readonly IWindowStateService _windowStateService;
    private readonly IServiceProvider _serviceProvider;
    private readonly IShutdownService _shutdownService;
    private readonly IAppConfig _config;
    private NativeMenu? _contextMenu;

    private bool _isInit;
    private MainWindow? _mainWindow;

    public StatusBarIconServicer(
        IThemeServicer themeServicer,
        IServiceProvider serviceProvider,
        IAppConfig appConfig,
        IWindowStateService windowStateService,
        IUIServicer uiServicer,
        IShutdownService shutdownService, IAppConfig config)
    {
        _serviceProvider = serviceProvider;
        _appConfig = appConfig;
        _windowStateService = windowStateService;
        _shutdownService = shutdownService;
        _config = config;
        _shutdownService.AddHandler(OnShuttingDown);
        _appConfig.ConfigChanged += OnConfigChanged;
    }

    public void Init()
    {
        var config = _appConfig.GetConfig();
        if (!config.General.IsEnableTray)
            return;

        Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (_trayIcon != null)
            {
                _trayIcon.IsVisible = true;
                return;
            }

            _trayIcon = new TrayIcon();
            SetIcon(IconType.Normal);
            InitMenu();
        });
    }

    public void ShowMainWindow()
    {
        var desk = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        if (desk == null) return;

        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        var mainVM = _serviceProvider.GetRequiredService<MainViewModel>();

        if (!_isInit)
        {
            _isInit = true;
            InitializeMainWindow(mainWindow, mainVM);
            desk.MainWindow = mainWindow;
            return;
        }

        if (desk.MainWindow == null)
        {
            InitializeMainWindow(mainWindow, mainVM);
            desk.MainWindow = mainWindow;
        }
        else
        {
            ShowExistingWindow(desk.MainWindow);
        }
    }

    private void SetIcon(IconType iconType = IconType.Normal)
    {
        try
        {
            if (_trayIcon == null) return;
            var iconName = iconType switch
            {
                IconType.Normal => "tai32",
                _ => "tai32"
            };
            _trayIcon.Icon =
                new WindowIcon(AssetLoader.Open(new Uri($"avares://Taix/Resources/Icons/{iconName}.ico")));
        }
        catch (Exception ex)
        {
            Logger.Error(ex.Message, ex);
            Logger.Flush();
        }
    }

    private void InitMenu()
    {
        _contextMenu = new NativeMenu();
        _trayIcon.Command = ReactiveCommand.Create(() => { ShowMainWindow(); });

        _contextMenu.Items.Add(new NativeMenuItem
        {
            Header = Application.Current?.FindResource("Open") as string,
            Command = ReactiveCommand.Create(() => { ShowMainWindow(); })
        });
        _contextMenu.Items.Add(new NativeMenuItem
        {
            Header = Application.Current?.FindResource("Exit") as string,
            Command = ReactiveCommand.CreateFromTask(ExitAppAsync)
        });
        Dispatcher.UIThread.Post(() => { _trayIcon.Menu = _contextMenu; });
    }

    private async Task ExitAppAsync()
    {
        if (_trayIcon != null)
            _trayIcon.IsVisible = false;
        await App.ExitAsync();
    }

    private async Task OnShuttingDown()
    {
        if (_mainWindow is null) return;

        var cfg = _appConfig.GetConfig();
        if (cfg.General.IsSaveWindowSize)
        {
            _windowStateService.WindowWidth = _mainWindow.Width;
            _windowStateService.WindowHeight = _mainWindow.Height;
            await _windowStateService.SaveAsync();
        }

        await _appConfig.SaveAsync();
    }

    private void InitializeMainWindow(MainWindow mainWindow, MainViewModel mainVM)
    {
        _mainWindow = mainWindow;

        var config = _appConfig.GetConfig();

        if (config.General.IsSaveWindowSize
            && _windowStateService.WindowWidth > 0
            && _windowStateService.WindowHeight > 0)
        {
            mainWindow.Width = _windowStateService.WindowWidth;
            mainWindow.Height = _windowStateService.WindowHeight;
        }

        mainWindow.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        mainWindow.WindowState = WindowState.Normal;
        mainWindow.DataContext = mainVM;

        mainWindow.Opened += (_, _) =>
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await _config.LoadAsync();
                    await mainVM.LoadDefaultPageAsync();
                    if (_appConfig.GetConfig().General.IsEnableTray)
                    {
                        Init();
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"加载配置失败: {ex.Message}", ex);
                }
            });

        };

        mainWindow.IsVisible = true;
        mainWindow.Closing += (s, e) =>
        {
            var cfg = _appConfig.GetConfig();
            if (!cfg.General.IsEnableTray)
                return;

            e.Cancel = true;
            mainWindow.IsVisible = false;
        };
    }

    private void ShowExistingWindow(Window existingWindow)
    {
        if (!existingWindow.IsVisible) existingWindow.IsVisible = true;

        if (existingWindow.WindowState == WindowState.Minimized) existingWindow.WindowState = WindowState.Normal;
        existingWindow.Activate();
    }

    private void OnConfigChanged(object? sender, Shared.Event.ConfigChangedEventArgs e)
    {
        if (!e.HasChange("General.IsEnableTray"))
            return;

        if (e.NewConfig.General.IsEnableTray)
        {
            Init();
        }
        else
        {
            if (_trayIcon != null)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (_trayIcon != null)
                    {
                        _trayIcon.IsVisible = false;
                        _trayIcon = null;
                    }
                });
            }
        }
    }

    public void Dispose()
    {
        _appConfig.ConfigChanged -= OnConfigChanged;
    }
}

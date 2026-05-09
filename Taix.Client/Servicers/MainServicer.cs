using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Taix.Client.Platform.Abstractions.Primitives;
using Taix.Client.Shared.Servicers.Interfaces;
using Taix.Client.ViewModels;
using Taix.Client.Views;

namespace Taix.Client.Servicers;

public class MainServicer : IMainServicer
{
    private readonly IAppContextMenuServicer _appContextMenuServicer;
    private readonly IAppConfig _config;
    private readonly IShutdownService _shutdownService;
    private readonly IThemeServicer _themeServicer;
    private readonly IWebSiteContextMenuServicer _webSiteContextMenuServicer;
    private readonly IWindowStateService _windowStateService;

    public MainServicer(
        IThemeServicer themeServicer,
        IAppContextMenuServicer appContextMenuServicer,
        IWebSiteContextMenuServicer webSiteContextMenuServicer,
        IWindowStateService windowStateService,
        IAppConfig config,
        IShutdownService shutdownService)
    {
        _themeServicer = themeServicer;
        _appContextMenuServicer = appContextMenuServicer;
        _webSiteContextMenuServicer = webSiteContextMenuServicer;
        _windowStateService = windowStateService;
        _config = config;
        _shutdownService = shutdownService;
    }

    public async Task Start()
    {
        SystemLanguage.InitializeLanguage(CultureCode.Auto);
        SystemLanguage.AttachConfig(_config);
        _themeServicer.Init();
        _appContextMenuServicer.Init();
        _webSiteContextMenuServicer.Init();

        _shutdownService.AddHandler(OnShuttingDown);

        ShowMainWindow();
        await _windowStateService.LoadAsync();
    }

    private void ShowMainWindow()
    {
        var desk = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        if (desk == null) return;

        var mainWindow = ServiceLocator.GetService<MainWindow>();
        var mainVM = ServiceLocator.GetService<MainViewModel>();

        var config = _config.GetConfig();

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
                }
                catch (Exception ex)
                {
                    Logging.Logger.Error($"加载配置失败: {ex.Message}", ex);
                }
            });
        };

        mainWindow.IsVisible = true;
        desk.MainWindow = mainWindow;
    }

    private async Task OnShuttingDown()
    {
        var desk = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        if (desk?.MainWindow is not MainWindow mainWindow) return;

        var cfg = _config.GetConfig();
        if (cfg.General.IsSaveWindowSize)
        {
            _windowStateService.WindowWidth = mainWindow.Width;
            _windowStateService.WindowHeight = mainWindow.Height;
            await _windowStateService.SaveAsync();
        }

        await _config.SaveAsync();
    }
}

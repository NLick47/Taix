using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Taix.Client.Platform.Abstractions.Primitives;
using Taix.Client.Shared.Servicers.Interfaces;
using Taix.Client.ViewModels;
using Taix.Client.Views;

namespace Taix.Client.Servicers;

public class MainServicer : IMainServicer
{
    private readonly IAppContextMenuServicer _appContextMenuServicer;
    private readonly IAppConfig _config;
    private readonly ICategorys _categorys;
    private readonly IShutdownService _shutdownService;
    private readonly IThemeServicer _themeServicer;
    private readonly IWebData _webData;
    private readonly IWebSiteContextMenuServicer _webSiteContextMenuServicer;
    private readonly IWindowStateService _windowStateService;

    public MainServicer(
        IThemeServicer themeServicer,
        IAppContextMenuServicer appContextMenuServicer,
        IWebSiteContextMenuServicer webSiteContextMenuServicer,
        IWindowStateService windowStateService,
        IAppConfig config,
        IShutdownService shutdownService,
        ICategorys categorys,
        IWebData webData)
    {
        _themeServicer = themeServicer;
        _appContextMenuServicer = appContextMenuServicer;
        _webSiteContextMenuServicer = webSiteContextMenuServicer;
        _windowStateService = windowStateService;
        _config = config;
        _shutdownService = shutdownService;
        _categorys = categorys;
        _webData = webData;
    }

    public async Task Start()
    {
        _shutdownService.AddHandler(OnShuttingDown);

        await Task.WhenAll(
            _windowStateService.LoadAsync(),
            _config.LoadAsync()
        );

        var language = (CultureCode)_config.GetConfig().General.Language;
        SystemLanguage.InitializeLanguage(language);
        SystemLanguage.AttachConfig(_config);

        _themeServicer.Init();

        _ = PreloadCategoriesAsync();

        ShowMainWindow();

        _appContextMenuServicer.Init();
        _webSiteContextMenuServicer.Init();
    }

    private async Task PreloadCategoriesAsync()
    {
        try
        {
            await Task.WhenAll(
                _categorys.GetCategoriesAsync(),
                _webData.GetWebSiteCategoriesAsync()
            );
        }
        catch (Exception ex)
        {
            Logging.Logger.Error($"预加载分类缓存失败: {ex.Message}", ex);
        }
    }

    private void ShowMainWindow()
    {
        var desk = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        if (desk == null) return;


        var mainWindow = new MainWindow();
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

        mainWindow.Opened += async (_, _) =>
        {
            try
            {
                mainVM.LoadDefaultPage();
                await Task.Delay(100);
            }
            catch (Exception ex)
            {
                Logging.Logger.Error($"加载默认页面失败: {ex.Message}", ex);
            }
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

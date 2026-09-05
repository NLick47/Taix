using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Taix.Client.Platform.Abstractions.Primitives;
using Taix.Client.Servicers.Instances;
using Taix.Client.Shared.Servicers.Interfaces;
using Taix.Client.ViewModels;
using Taix.Client.Views;

namespace Taix.Client.Servicers;

public class MainServicer : IMainServicer
{
    private readonly IContextMenuServicer _contextMenuServicer;
    private readonly IAppConfig _config;
    private readonly ICategorys _categorys;
    private readonly IShutdownService _shutdownService;
    private readonly IThemeServicer _themeServicer;
    private readonly IWebData _webData;
    private readonly WindowStateTracker _windowStateTracker;

    public MainServicer(
        IThemeServicer themeServicer,
        IContextMenuServicer contextMenuServicer,
        WindowStateTracker windowStateTracker,
        IAppConfig config,
        IShutdownService shutdownService,
        ICategorys categorys,
        IWebData webData)
    {
        _themeServicer = themeServicer;
        _contextMenuServicer = contextMenuServicer;
        _windowStateTracker = windowStateTracker;
        _config = config;
        _shutdownService = shutdownService;
        _categorys = categorys;
        _webData = webData;
    }

    public async Task Start()
    {
        _shutdownService.AddHandler(OnShuttingDown);

        // 缓存命中时两者都是同步返回，直接顺序触发，省掉 WhenAll 的状态机开销
        var configLoad = _config.LoadAsync();
        if (!configLoad.IsCompleted)
        {
            await configLoad;
        }

        var language = (CultureCode)_config.GetConfig().General.Language;
        SystemLanguage.InitializeLanguage(language);
        SystemLanguage.AttachConfig(_config);

        _themeServicer.Init();

        _ = PreloadCategoriesAsync();

        ShowMainWindow();

        // 上下文菜单 Init 推迟到渲染回合之后，让 Start() 尽早返回；用 Send 不会被后台任务插队
        _ = Dispatcher.UIThread.InvokeAsync(() =>
        {
            _contextMenuServicer.Init();
        });
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

        mainWindow.DataContext = mainVM;

        mainWindow.Opened += (_, _) =>
        {
            try
            {
                mainVM.LoadDefaultPage();
            }
            catch (Exception ex)
            {
                Logging.Logger.Error($"加载默认页面失败: {ex.Message}", ex);
            }
        };

        _windowStateTracker.Attach(mainWindow);

        // 先挂 MainWindow 再设可见：部分 Avalonia 逻辑依赖 MainWindow 就绪（关闭策略、Activate 路径）
        desk.MainWindow = mainWindow;
        mainWindow.IsVisible = true;
    }

    private async Task OnShuttingDown()
    {
        await _config.SaveAsync();
    }
}

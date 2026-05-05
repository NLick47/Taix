using System.Threading.Tasks;
using Taix.Client.Platform.Abstractions.Primitives;
using Taix.Client.Shared.Servicers.Interfaces;

namespace Taix.Client.Servicers;

public class MainServicer : IMainServicer
{
    private readonly IAppContextMenuServicer _appContextMenuServicer;
    private readonly IAppConfig _config;
    private readonly IStatusBarIconServicer _statusBarIconServicer;
    private readonly IThemeServicer _themeServicer;
    private readonly IWebSiteContextMenuServicer _webSiteContextMenuServicer;
    private readonly IWindowStateService _windowStateService;

    public MainServicer(
        IThemeServicer themeServicer,
        IStatusBarIconServicer statusBarIconServicer,
        IAppContextMenuServicer appContextMenuServicer,
        IWebSiteContextMenuServicer webSiteContextMenuServicer,
        IWindowStateService windowStateService,
        IAppConfig config)
    {
        _themeServicer = themeServicer;
        _appContextMenuServicer = appContextMenuServicer;
        _statusBarIconServicer = statusBarIconServicer;
        _webSiteContextMenuServicer = webSiteContextMenuServicer;
        _windowStateService = windowStateService;
        _config = config;
    }

    public async Task Start()
    {
        SystemLanguage.InitializeLanguage(CultureCode.Auto);
        SystemLanguage.AttachConfig(_config);
        _themeServicer.Init();
        _appContextMenuServicer.Init();
        _webSiteContextMenuServicer.Init();
        _statusBarIconServicer.ShowMainWindow();
        await _windowStateService.LoadAsync();
    }
}

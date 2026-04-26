using System;
using System.Threading.Tasks;
using Taix.Client.Logging;
using Taix.Client.Platform.Abstractions.Primitives;
using Taix.Client.Shared.Servicers.Interfaces;

namespace Taix.Client.Servicers;

public class MainServicer : IMainServicer
{
    private readonly IAppContextMenuServicer _appContextMenuServicer;
    private readonly IAppConfig _config;
    private readonly IAppData _appData;
    private readonly ICategorys _categorys;
    private readonly IStatusBarIconServicer _statusBarIconServicer;
    private readonly IThemeServicer _themeServicer;
    private readonly IWebSiteContextMenuServicer _webSiteContext;
    public MainServicer(
        IThemeServicer themeServicer,
        IStatusBarIconServicer statusBarIconServicer,
        IAppContextMenuServicer appContextMenuServicer,
        IWebSiteContextMenuServicer webSiteContext,
        IAppConfig config,
        IAppData appData,
        ICategorys categorys)
    {
        _themeServicer = themeServicer;
        _appContextMenuServicer = appContextMenuServicer;
        _webSiteContext = webSiteContext;
        _statusBarIconServicer = statusBarIconServicer;
        _config = config;
        _appData = appData;
        _categorys = categorys;
    }

    public async Task Start()
    {
        try
        {
            await _config.LoadAsync();
        }
        catch (Exception ex)
        {
            Logger.Error($"加载配置失败: {ex.Message}", ex);
        }

        ConfigLoaded();

        try
        {
            await _appData.LoadAsync();
            await _categorys.LoadAsync();
        }
        catch (Exception ex)
        {
            Logger.Error($"加载初始数据失败: {ex.Message}", ex);
        }

        _statusBarIconServicer.Init();
    }

    public void DesignStart()
    {
        SystemLanguage.InitializeLanguage(CultureCode.Auto);
        _themeServicer.Init();
        _appContextMenuServicer.Init();
        _webSiteContext.Init();
    }

    private void ConfigLoaded()
    {
        SystemLanguage.InitializeLanguage((CultureCode)_config.GetConfig().General.Language);
        _themeServicer.Init();
        _appContextMenuServicer.Init();
        _webSiteContext.Init();
        _statusBarIconServicer.ShowMainWindow();
    }
}

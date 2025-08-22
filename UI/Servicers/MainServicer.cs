using System;
using System.Threading.Tasks;
using Core.Servicers.Interfaces;
using SharedLibrary;
using SharedLibrary.Enums;
using SharedLibrary.Servicers;

namespace UI.Servicers;

public class MainServicer : IMainServicer
{
    private readonly IAppContextMenuServicer _appContextMenuServicer;
    private readonly IAppConfig _config;
    private readonly IMain _main;
    private readonly IStatusBarIconServicer _statusBarIconServicer;
    private readonly ISystemInfrastructure _systemInfrastructure;
    private readonly IThemeServicer _themeServicer;
    private readonly IWebSiteContextMenuServicer _webSiteContext;
    private bool isSelfStart;

    public MainServicer(IMain main,
        IThemeServicer themeServicer,
        IStatusBarIconServicer statusBarIconServicer_,
        IAppContextMenuServicer appContextMenuServicer,
        IWebSiteContextMenuServicer webSiteContext_,
        IAppConfig config_, ISystemInfrastructure systemInfrastructure_)
    {
        _main = main;
        _themeServicer = themeServicer;
        _appContextMenuServicer = appContextMenuServicer;
        _webSiteContext = webSiteContext_;
        _statusBarIconServicer = statusBarIconServicer_;
        _config = config_;
        _systemInfrastructure = systemInfrastructure_;
    }

    public Task Start(bool isSelfStart)
    {
        this.isSelfStart = isSelfStart;
        _main.OnStarted += Main_OnStarted;
        _main.OnConfigLoaded += ConfigLoaded;
        return Task.WhenAll(_main.Run(), _statusBarIconServicer.Init());
    }

    public async void DesignStart()
    {
        _main.OnStarted += Main_DesignStarted;
        await _main.Run();
    }

    private void ConfigLoaded(object sender, EventArgs e)
    {
        if (!isSelfStart && _config.GetConfig().General.IsStartatboot) _systemInfrastructure.SetStartup(true);
    }

    private void Main_DesignStarted(object sender, EventArgs e)
    {
        SystemLanguage.InitializeLanguage(CultureCode.Auto);
        _themeServicer.Init();
        _appContextMenuServicer.Init();
        _webSiteContext.Init();
    }

    private void Main_OnStarted(object sender, EventArgs e)
    {
        SystemLanguage.InitializeLanguage((CultureCode)_config.GetConfig().General.Language);
        _themeServicer.Init();
        _appContextMenuServicer.Init();
        _webSiteContext.Init();
        if (!isSelfStart) _statusBarIconServicer.ShowMainWindow();
    }
}
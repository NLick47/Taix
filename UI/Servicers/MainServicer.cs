using Avalonia;
using Core;
using SharedLibrary.Enums;
using Core.Servicers.Interfaces;
using SharedLibrary.Servicers;
using SharedLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SharedLibrary.Librarys;
using System.Reflection;
using UI.Servicers.Updater;
using Avalonia.Controls.ApplicationLifetimes;
using UI.Models;
using UI.Controls.Window;

namespace UI.Servicers
{
    public class MainServicer : IMainServicer
    {
        private readonly IMain main;
        private readonly IThemeServicer themeServicer;
        private readonly IAppContextMenuServicer appContextMenuServicer;
        private readonly IWebSiteContextMenuServicer _webSiteContext;
        private readonly IStatusBarIconServicer _statusBarIconServicer;
        private readonly IAppConfig _config;
        private readonly ISystemInfrastructure _systemInfrastructure;
        private bool isSelfStart = false;

        public MainServicer(IMain main,
           IThemeServicer themeServicer,
            IStatusBarIconServicer statusBarIconServicer_,
             IAppContextMenuServicer appContextMenuServicer,
            IWebSiteContextMenuServicer webSiteContext_,
            IAppConfig config_, ISystemInfrastructure systemInfrastructure_)
        {
            this.main = main;
            this.themeServicer = themeServicer;
            this.appContextMenuServicer = appContextMenuServicer;
            _webSiteContext = webSiteContext_;
            _statusBarIconServicer = statusBarIconServicer_;
            _config = config_;
            _systemInfrastructure = systemInfrastructure_;
        }
        public Task Start(bool isSelfStart)
        {
            this.isSelfStart = isSelfStart;
            main.OnStarted += Main_OnStarted;
            main.OnConfigLoaded += ConfigLoaded;
            return Task.WhenAll(main.Run(), _statusBarIconServicer.Init());
        }

        private void ConfigLoaded(object sender, EventArgs e)
        {
            if (!isSelfStart && _config.GetConfig().General.IsStartatboot)
            {
                _systemInfrastructure.SetStartup(true);
            }
        }
        
        private void Main_OnStarted(object sender, EventArgs e)
        {
            SystemLanguage.InitializeLanguage((CultureCode)_config.GetConfig().General.Language);
            themeServicer.Init();
            appContextMenuServicer.Init();
            _webSiteContext.Init();
            if (!isSelfStart)
            {
                _statusBarIconServicer.ShowMainWindow();
            }
        }
    }
}

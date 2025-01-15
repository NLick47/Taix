using Core;
using Core.Enums;
using Core.Servicers.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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
        private bool isSelfStart = false;

        public MainServicer(IMain main,
           IThemeServicer themeServicer,
            IStatusBarIconServicer statusBarIconServicer_,
             IAppContextMenuServicer appContextMenuServicer,
            IWebSiteContextMenuServicer webSiteContext_,
            IAppConfig config_)
        {
            this.main = main;
            this.themeServicer = themeServicer;
            this.appContextMenuServicer = appContextMenuServicer;
            _webSiteContext = webSiteContext_;
            _statusBarIconServicer = statusBarIconServicer_;
            _config = config_;
        }
        public  Task Start(bool isSelfStart)
        {
            this.isSelfStart = isSelfStart;
            main.OnStarted += Main_OnStarted;
            return Task.WhenAll(main.Run(), _statusBarIconServicer.Init());
        }

        private void Main_OnStarted(object sender, EventArgs e)
        {

            SystemLanguage.InitializedLanguage((CultureCode)_config.GetConfig().General.Language);
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

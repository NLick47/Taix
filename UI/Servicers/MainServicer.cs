using Core;
using Core.Servicers.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UI.Servicers
{
    public class MainServicer : IMainServicer
    {
        private readonly IMain main;
        private readonly IThemeServicer themeServicer;
        //private readonly IAppContextMenuServicer appContextMenuServicer;
        //private readonly IWebSiteContextMenuServicer _webSiteContext;
        private readonly IStatusBarIconServicer _statusBarIconServicer;
        private readonly IAppConfig _config;
        private bool isSelfStart = false;

        public MainServicer(IMain main,
           IThemeServicer themeServicer,
            IStatusBarIconServicer statusBarIconServicer_,
            IAppConfig config_)
        {
            this.main = main;
            this.themeServicer = themeServicer;
            _statusBarIconServicer = statusBarIconServicer_;
            _config = config_;
        }
        public async Task Start(bool isSelfStart)
        {
            this.isSelfStart = isSelfStart;
            main.OnStarted += Main_OnStarted;
            //main.Run() 方法有问题，会让ui状态发生异常行为。之后再处理
            AppState.IsLoading = false;
            //await main.Run();
            await _statusBarIconServicer.Init();
            //await Task.WhenAll(main.Run(), _statusBarIconServicer.Init());
        }

        private void Main_OnStarted(object sender, EventArgs e)
        {
            themeServicer.Init();
            //inputServicer.Start();
            //appContextMenuServicer.Init();
            //_webSiteContext.Init();
            //if (!isSelfStart && _config.GetConfig().General.IsStartupShowMainWindow)
            //{
            //    _statusBarIconServicer.ShowMainWindow();
            //}
        }
    }
}

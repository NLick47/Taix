using Infrastructure.Servicers;
using Microsoft.Extensions.DependencyInjection;
using Platform;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Win
{
    public class WinPlatformInitializer : IPlatformInitializer
    {
        public void Initialize(IServiceCollection services)
        {
            services.AddSingleton<ISleepdiscover, WinSleepdiscover>();
            services.AddSingleton<IAppObserver, WinAppObserver>();
            services.AddSingleton<IAppManager, WinAppManager>();
            services.AddSingleton<IWindowManager, WindowManager>();
            services.AddSingleton<ISystemInfrastructure, WinSystemInfrastructure>();
        }
    }
}

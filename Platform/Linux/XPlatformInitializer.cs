using SharedLibrary.Servicers;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Linux
{
    public class XPlatformInitializer : IPlatformInitializer
    {
        public void Initialize(IServiceCollection services)
        {
            services.AddSingleton<ISleepdiscover, XSleepdiscover>();
            services.AddSingleton<IAppObserver, XAppObserver>();
            services.AddSingleton<IAppManager, XAppManager>();
            services.AddSingleton<IWindowManager, XManager>();
            services.AddSingleton<ISystemInfrastructure, XSystemInfrastructure>();
        }
    }
}

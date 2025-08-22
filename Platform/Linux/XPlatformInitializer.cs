using Microsoft.Extensions.DependencyInjection;
using SharedLibrary.Servicers;

namespace Linux;

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
using Microsoft.Extensions.DependencyInjection;
using SharedLibrary.Servicers;

namespace Win;

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
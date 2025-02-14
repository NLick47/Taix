using Core.Servicers;
using Core.Servicers.Instances;
using Core.Servicers.Interfaces;
using SharedLibrary.Librarys;
using SharedLibrary.Servicers;
using Win;

namespace Server;

public class Program 
{
    public static void Main(string[] args)
    {
        CreateHostBuilder(args).Build().Run();
        AppDomain.CurrentDomain.UnhandledException += (sender, args) => Logger.Save(true);
    }
    
    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureServices((hostContext, services) =>
            {
                services.AddSingleton<IAppTimerServicer, AppTimerServicer>();
                services.AddSingleton<IWebServer, WebServer>();
                services.AddSingleton<IMain, Main>();
                services.AddSingleton<IData, Data>();
                services.AddSingleton<IWebData, WebData>();
                services.AddSingleton<IAppConfig, AppConfig>();
                services.AddSingleton<IDateTimeObserver, DateTimeObserver>();
                services.AddSingleton<IAppData, AppData>();
                services.AddSingleton<ICategorys, Categorys>();
                services.AddSingleton<IWebFilter, WebFilter>();
                CreatePlatformInitializer(services);
               services.AddHostedService<Worker>();
            });


    private static void CreatePlatformInitializer(IServiceCollection services)
    {
        IPlatformInitializer platformInitializer = null;
#if WINDOWS
            platformInitializer = new Win.WinPlatformInitializer();
#elif LINUX
            platformInitializer = new Linux.XPlatformInitializer();
#elif MACOS
#endif

        platformInitializer.Initialize(services);
    }

}
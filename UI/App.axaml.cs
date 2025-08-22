using System;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Core.Servicers;
using Core.Servicers.Instances;
using Core.Servicers.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using SharedLibrary.Librarys;
using SharedLibrary.Servicers;
using UI.Servicers;
using UI.Servicers.Updater;
using UI.ViewModels;
using UI.Views;
using Win;

namespace UI;

public class App : Application
{
    private Mutex mutex;

    private void CreatePlatformInitializer(IServiceCollection services)
    {
        IPlatformInitializer platformInitializer = null;
#if WINDOWS
        platformInitializer = new WinPlatformInitializer();
#elif LINUX
            platformInitializer = new Linux.XPlatformInitializer();
#elif MACOS
#endif

        platformInitializer.Initialize(services);
    }

    private void ConfigureServices(IServiceCollection services)
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

        services.AddSingleton<IUIServicer, UIServicer>();
        services.AddSingleton<IAppContextMenuServicer, AppContextMenuServicer>();
        services.AddSingleton<IThemeServicer, ThemeServicer>();
        services.AddSingleton<IMainServicer, MainServicer>();
        //services.AddSingleton<IInputServicer, InputServicer>();
        services.AddSingleton<IWebSiteContextMenuServicer, WebSiteContextMenuServicer>();
        services.AddSingleton<IStatusBarIconServicer, StatusBarIconServicer>();

        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();

        services.AddTransient<IndexPage>();
        services.AddTransient<IndexPageViewModel>();

        services.AddTransient<DataPage>();
        services.AddTransient<DataPageViewModel>();

        services.AddTransient<SettingPage>();
        services.AddTransient<SettingPageViewModel>();

        services.AddTransient<DetailPage>();
        services.AddTransient<DetailPageViewModel>();

        services.AddTransient<CategoryPage>();
        services.AddTransient<CategoryPageViewModel>();

        services.AddTransient<CategoryAppListPage>();
        services.AddTransient<CategoryAppListPageViewModel>();

        services.AddTransient<CategoryWebSiteListPage>();
        services.AddTransient<CategoryWebSiteListPageViewModel>();

        services.AddTransient<ChartPage>();
        services.AddTransient<ChartPageViewModel>();

        services.AddTransient<WebSiteDetailPage>();
        services.AddTransient<WebSiteDetailPageViewModel>();

        services.AddTransient<UpdateCheckerService>();
    }

    private bool IsRunned()
    {
#if DEBUG
        return false;
#endif
        var mutexName = "Taix";
        bool createdNew;
        mutex = new Mutex(true, mutexName, out createdNew);
        return !createdNew;
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        Dispatcher.UIThread.UnhandledException += (sender, e) =>
        {
            Logger.Error("[Program crash] " + e.Exception.Message);
            Logger.Error("Stack trace: " + e.Exception.StackTrace);
            Logger.Save(true);
            new ErrorDialog().Show();
            e.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
        {
            var exception = e.ExceptionObject as Exception;
            Logger.Error("[Program crash]" + exception.Message);
            Logger.Save(true);
        };
        var serviceCollection = new ServiceCollection();
        CreatePlatformInitializer(serviceCollection);
        ConfigureServices(serviceCollection);
        ServiceLocator.Initialize(serviceCollection.BuildServiceProvider());

        if (Design.IsDesignMode)
        {
            var main = ServiceLocator.GetService<IMainServicer>();
            main.DesignStart();
        }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            OnStartup(this, Environment.GetCommandLineArgs());
            desktop.Exit += (e, r) => { Logger.Save(true); };
        }

        base.OnFrameworkInitializationCompleted();
    }


    private async void OnStartup(object sender, string[] args)
    {
        if (IsRunned()) Environment.Exit(0);
        var main = ServiceLocator.GetService<IMainServicer>();

        var isSelfStart = false;
        if (args.Length > 1)
            if (args[1].Equals("--selfStart"))
                isSelfStart = true;

        try
        {
            await main.Start(isSelfStart);
        }
        catch (Exception e)
        {
            Logger.Error(e.Message);
            Logger.Save(true);
        }
    }


    public static void Exit()
    {
        var desktop = Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        desktop?.Shutdown();
    }
}
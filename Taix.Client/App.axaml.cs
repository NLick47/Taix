using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Taix.Client.Librarys.Api;
using Taix.Client.Logging;
using Taix.Client.Servicers;
using Taix.Client.Servicers.Instances;
using Taix.Client.Servicers.Interfaces;
using Taix.Client.Servicers.Updater;
using Taix.Client.Shared.Servicers.Interfaces;
using Taix.Client.Shared.Servicers.Interfaces;
using Taix.Client.ViewModels;
using Taix.Client.Views;

namespace Taix.Client;

public class App : Application
{
    private Mutex mutex;

    private void ConfigureServices(IServiceCollection services)
    {
        services.AddHttpClient("TaixApi", client =>
        {
            var serverUrl = Environment.GetEnvironmentVariable("TAIX_SERVER") ?? "http://localhost:37091";
            client.BaseAddress = new Uri(serverUrl);
            client.Timeout = TimeSpan.FromSeconds(6);
        });

        services.AddSingleton<ITaixApiClient, TaixApiClient>();

        services.AddSingleton<IData, ApiData>();
        services.AddSingleton<IWebData, ApiWebData>();
        services.AddSingleton<IAppData, ApiAppData>();
        services.AddSingleton<IWebSiteData, ApiWebSiteData>();
        services.AddSingleton<ICategorys, ApiCategorys>();
        services.AddSingleton<IAppConfig, ApiAppConfig>();
        services.AddSingleton<IWindowStateService, WindowStateService>();

        services.AddSingleton<IUIServicer, UIServicer>();
        services.AddSingleton<IDialogService>(provider => provider.GetRequiredService<IUIServicer>() as IDialogService);
        services.AddSingleton<IClipboardService, ClipboardService>();
        services.AddSingleton<IProcessService, ProcessService>();
        services.AddSingleton<IAppContextMenuServicer, AppContextMenuServicer>();
        services.AddSingleton<IThemeServicer, ThemeServicer>();
        services.AddSingleton<IMainServicer, MainServicer>();

        services.AddSingleton<IWebSiteContextMenuServicer, WebSiteContextMenuServicer>();
        services.AddSingleton<IStatusBarIconServicer, StatusBarIconServicer>();
        services.AddSingleton<IShutdownService, ShutdownService>();

        services.AddSingleton<MainViewModel>();
        services.AddSingleton<INavigationService>(provider => provider.GetRequiredService<MainViewModel>());
        services.AddSingleton<INavigationDataService>(provider => provider.GetRequiredService<MainViewModel>());
        services.AddSingleton<IToastService>(provider => provider.GetRequiredService<MainViewModel>());
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
        Logger.SetLogger(new FileLogger(options =>
        {
            options.MaxLogFileAgeDays = 30;
            options.SaveThreshold = 100;
            options.AutoSaveInterval = 1000 * 60 * 10;
            options.WriteToConsole = true;
        }));

        Dispatcher.UIThread.UnhandledException += (sender, e) =>
        {
            Logger.Error("[Program crash] " + e.Exception.Message, e.Exception);
            Logger.Error("Stack trace", e.Exception);
            Logger.Flush();
            new Views.Dialogs.ErrorDialog().Show();
            e.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
        {
            var exception = e.ExceptionObject as Exception;
            Logger.Error("[Program crash]" + exception.Message, exception);
            Logger.Flush();
        };
        var serviceCollection = new ServiceCollection();
        ConfigureServices(serviceCollection);
        ServiceLocator.Initialize(serviceCollection.BuildServiceProvider());


        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            OnStartup(this, Environment.GetCommandLineArgs());
            desktop.Exit += (e, r) =>
            {
                Logger.Flush();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private async void OnStartup(object sender, string[] args)
    {
        try
        {
            await OnStartupAsync(sender, args);
        }
        catch (Exception e)
        {
            Logger.Error(e.Message, e);
            Logger.Flush();
        }
    }

    private async Task OnStartupAsync(object sender, string[] args)
    {
        if (IsRunned()) Environment.Exit(0);
        var main = ServiceLocator.GetService<IMainServicer>();
        await main.Start();
    }


    private static bool _isShuttingDown;
    public static bool IsShuttingDown => _isShuttingDown;

    public static async Task ExitAsync()
    {
        if (_isShuttingDown) return;
        _isShuttingDown = true;

        var desktop = Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        if (desktop == null) return;

        var shutdownService = ServiceLocator.GetService<IShutdownService>();
        if (shutdownService != null)
            await shutdownService.ShutdownAsync();

        desktop.Shutdown();
    }
}

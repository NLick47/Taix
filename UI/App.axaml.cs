using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using SharedLibrary.Servicers;
using Core.Librarys;
using Core.Servicers.Instances;
using Core.Servicers.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using UI.ViewModels;
using UI.Views;
using SharedLibrary.Librarys;
using System.Reflection;
using Avalonia.Media;
using UI.Servicers;
using UI.Controls.Window;
using Avalonia.Controls;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Threading;
using Core.Servicers;
using UI.Models;
using ReactiveUI;
using Avalonia.Threading;



namespace UI
{
    public partial class App : Application
    {
        private  readonly ServiceProvider serviceProvider;
        private System.Threading.Mutex mutex;
        private HideWindow keepaliveWindow;


        public static ServiceProvider ServiceProvider => Instance.serviceProvider;
        private static App Instance;

        public App()
        {
            Instance = this;
            var serviceCollection = new ServiceCollection();
            CreatePlatformInitializer(serviceCollection);
            ConfigureServices(serviceCollection);
            serviceProvider = serviceCollection.BuildServiceProvider();
        }

       

        private void CreatePlatformInitializer(IServiceCollection services)
        {
            //string platfromName = PlatformInfo.GetPlatformName();
            //string dllPath = Path.Combine(AppContext.BaseDirectory, $"{platfromName}.dll");
            //var assembly = Assembly.LoadFile(dllPath);
            //var classType = assembly.GetType($"{platfromName}.WinPlatformInitializer");
            //var Instance = Activator.CreateInstance(classType!);
            //var methodInfo = classType!.GetMethod("Initialize");
            //methodInfo!.Invoke(Instance, new object[] { services });
            IPlatformInitializer platformInitializer = null;
            #if WINDOWS
            platformInitializer = new Win.WinPlatformInitializer();
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
        }

        private bool IsRunned()
        {
            var mutexName = "Taix"; 
            bool createdNew;
            mutex = new System.Threading.Mutex(true, mutexName, out createdNew);
            return !createdNew;
        }

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override  async void OnFrameworkInitializationCompleted()
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
         
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                if (Avalonia.Controls.Design.IsDesignMode)
                {
                    desktop.MainWindow = new MainWindow()
                    {
                        DataContext = App.ServiceProvider.GetService<MainViewModel>()
                    };
                    return;
                }
                OnStartup(this, Environment.GetCommandLineArgs());
                desktop.Exit += (e, r) =>
                {
                    Logger.Save(true);
                };
            
            }
            base.OnFrameworkInitializationCompleted();
        }


        private  async void OnStartup(object sender, string[] args)
        {
            if (IsRunned())
            {
                Environment.Exit(0);
            }
            var main = serviceProvider.GetService<IMainServicer>();

            bool isSelfStart = false;
            if (args.Length > 1)
            {
                if (args[1].Equals("--selfStart"))
                {
                    isSelfStart = true;
                }
            }
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
            var desktop = Application.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
            desktop?.Shutdown();
        }

        
    }
}
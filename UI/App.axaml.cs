using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Infrastructure.Servicers;
using Core.Librarys;
using Core.Servicers.Instances;
using Core.Servicers.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using UI.ViewModels;
using UI.Views;
using Infrastructure.Librarys;
using System.Reflection;
using Avalonia.Media;
using UI.Servicers;
using UI.Controls.Window;
using Platform;
using Avalonia.Controls;
using System.Threading.Tasks;

namespace UI
{
    public partial class App : Application
    {
        private readonly ServiceProvider serviceProvider;
        private System.Threading.Mutex mutex;
        private HideWindow keepaliveWindow;
        private static IClassicDesktopStyleApplicationLifetime? AppLife;

        public App()
        {
            AppDomain.CurrentDomain.UnhandledException += App_DispatcherUnhandledException;
#if DEBUG
            AppDomain.CurrentDomain.UnhandledException -= App_DispatcherUnhandledException;

#endif
            var serviceCollection = new ServiceCollection();
            CreatePlatformInitializer(serviceCollection);
            ConfigureServices(serviceCollection);
            serviceProvider = serviceCollection.BuildServiceProvider();
        }

        private void CreatePlatformInitializer(IServiceCollection services)
        {
            string platfromName = string.Empty;
#if WINDOWS
            platfromName = "Win";

#endif
            string dllPath = Path.Combine(AppContext.BaseDirectory, $"{platfromName}.dll");
            var assembly = Assembly.LoadFile(dllPath);
            var classType = assembly.GetType($"{platfromName}.WinPlatformInitializer");
            var Instance = Activator.CreateInstance(classType!);
            var methodInfo = classType!.GetMethod("Initialize");
            methodInfo!.Invoke(Instance, new object[] { services });
        }

        private void ConfigureServices(IServiceCollection services)
        {
            //  ���ķ���
            services.AddSingleton<IDatabase, Database>();
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

            //  UI����
            services.AddSingleton<IUIServicer, UIServicer>();
            //services.AddSingleton<IAppContextMenuServicer, AppContextMenuServicer>();
            services.AddSingleton<IThemeServicer, ThemeServicer>();
            services.AddSingleton<IMainServicer, MainServicer>();
            //services.AddSingleton<IInputServicer, InputServicer>();
            //services.AddSingleton<IWebSiteContextMenuServicer, WebSiteContextMenuServicer>();
            services.AddSingleton<IStatusBarIconServicer, StatusBarIconServicer>();

            services.AddSingleton<MainViewModel>();
            services.AddSingleton<MainWindow>();
            ////  ��ҳ
            //services.AddTransient<IndexPage>();
            //services.AddTransient<IndexPageVM>();

            ////  ����ҳ
            //services.AddTransient<DataPage>();
            //services.AddTransient<DataPageVM>();

            ////  ����ҳ
            //services.AddTransient<SettingPage>();
            //services.AddTransient<SettingPageVM>();

            ////  ����ҳ
            //services.AddTransient<DetailPage>();
            //services.AddTransient<DetailPageVM>();

            ////  ����
            //services.AddTransient<CategoryPage>();
            //services.AddTransient<CategoryPageVM>();

            ////  ����app
            //services.AddTransient<CategoryAppListPage>();
            //services.AddTransient<CategoryAppListPageVM>();
            ////  ����վ��
            //services.AddTransient<CategoryWebSiteListPage>();
            //services.AddTransient<CategoryWebSiteListPageVM>();
            ////  ͼ��
            //services.AddTransient<ChartPage>();
            //services.AddTransient<ChartPageVM>();
            ////  ��վ����
            //services.AddTransient<WebSiteDetailPage>();
            //services.AddTransient<WebSiteDetailPageVM>();
        }


        private void App_DispatcherUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Logger.Error("[Program crash]" + e.ExceptionObject.ToString());
            Logger.Save(true);
            string taiBugPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "TaiBug.exe");
            ProcessHelper.Run(taiBugPath, new string[] { string.Empty });

        }

        
        

        private bool IsRuned()
        {
            bool ret;
            mutex = new System.Threading.Mutex(true, System.Reflection.Assembly.GetEntryAssembly().ManifestModule.Name, out ret);
            if (!ret)
            {
#if !DEBUG
                return true;

#endif
            }
            return false;
        }
        
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override async void OnFrameworkInitializationCompleted()
        {
           
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                void ShowMainWindow()
                {
                    var window = serviceProvider.GetService<MainWindow>();
                    window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                    window.WindowState = WindowState.Normal;
                    window.DataContext = serviceProvider.GetService<MainViewModel>();
                    desktop.MainWindow = window;
                }
                ShowMainWindow();
                await OnStartup(this, Environment.GetCommandLineArgs());
                desktop.Exit += (e, r) =>
                {
                    Logger.Save(true);
                };
                AppLife = desktop;
            }
          
            base.OnFrameworkInitializationCompleted();
        }


        private async Task OnStartup(object sender, string[] args)
        {
            //  ��ֹ�࿪����
            if (IsRuned())
            {
                Exit();
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
           await main.Start(isSelfStart);

            //  ���������
            //keepaliveWindow = new HideWindow();
            //keepaliveWindow.Hide();
        }


        public static void Exit()
        {
            AppLife?.Shutdown();
        }

        
    }
}
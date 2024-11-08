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

namespace UI
{
    public partial class App : Application
    {
        private readonly ServiceProvider serviceProvider;
        private System.Threading.Mutex mutex;
        
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

        public override void OnFrameworkInitializationCompleted()
        {
           
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = new MainWindow
                {
                    DataContext = new MainViewModel(),
                };
            }
          
            base.OnFrameworkInitializationCompleted();
        }
    }
}
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using System.Threading;
using System;
using System.Diagnostics.Metrics;
using System.Runtime.InteropServices;

namespace TaixBug
{
    public partial class App : Application
    {
        private System.Threading.Mutex _mutex;


        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void OnStartup(object sender, ControlledApplicationLifetimeStartupEventArgs e)
        {
            bool createdNew;
            var mutexName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "TaixBug.exe" : "TaixBug";
            _mutex = new Mutex(true, mutexName, out createdNew);
            var desk = ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
            if (e.Args.Length == 0 || !createdNew)
            {
                desk.Shutdown();
            }
            else
            {
                desk.MainWindow = new MainWindow();
            }

        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.Startup += OnStartup;

            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}
using System;
using System.Diagnostics;
using System.IO;
using Avalonia;
using ReactiveUI.Avalonia;
using Velopack;

namespace Taix.Client;

internal sealed class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        VelopackApp.Build()
            .OnAfterInstallFastCallback(_ => StartTaixShell())
            .OnAfterUpdateFastCallback(_ => StartTaixShell())
            .OnBeforeUninstallFastCallback(_ => StopTaixShell())
            .Run();
        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    private static void StartTaixShell()
    {
        try
        {
            var shellExe = Path.Combine(AppContext.BaseDirectory, "taix-shell.exe");
            if (!File.Exists(shellExe)) return;

            var dataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Taix");

            Directory.CreateDirectory(dataDir);

            Process.Start(new ProcessStartInfo(shellExe)
            {
                WorkingDirectory = dataDir,
                UseShellExecute = true
            });
        }
        catch
        {
            // Fast callback must not block; ignore startup failures
        }
    }

    private static void StopTaixShell()
    {
        try
        {
            // Ask taix-shell to clean up its scheduled task and registry autostart
            var shellExe = Path.Combine(AppContext.BaseDirectory, "taix-shell.exe");
            if (File.Exists(shellExe))
            {
                using var proc = Process.Start(new ProcessStartInfo(shellExe, "uninstall")
                {
                    WorkingDirectory = AppContext.BaseDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                proc?.WaitForExit(3000);
            }

            // Kill any remaining taix-shell processes
            foreach (var proc in Process.GetProcessesByName("taix-shell"))
            {
                try { proc.Kill(); } catch { }
                proc.Dispose();
            }
        }
        catch
        {
            // Fast callback must not block; ignore cleanup failures
        }
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .UseReactiveUI(_ => { });
    }
}

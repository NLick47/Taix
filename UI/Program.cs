using System;
using Avalonia;
using Avalonia.ReactiveUI;

namespace UI;

internal sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
//#if DEBUG
//            DebugMovePlatfromDll();
//#endif
        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace()
            .UseReactiveUI();
    }

    //#if DEBUG
//        private static void DebugMovePlatfromDll()
//        {
//            var platformName = PlatformInfo.GetPlatformName();
//            string directory = AppContext.BaseDirectory;
//            while (!string.IsNullOrEmpty(directory))
//            {
//                if (Directory.Exists(Path.Combine(directory, "Platform")))
//                {
//                    directory = Path.Combine(directory, "Platform");
//                    break;
//                }
//                directory = Directory.GetParent(directory)?.FullName;
//            }
//            if (string.IsNullOrEmpty(directory))
//            {
//                throw new DirectoryNotFoundException(directory);
//            }
//            var dll = string.Concat(platformName, ".dll");
//            var targetFilePath = Path.Combine(directory, platformName, "bin", "Debug", "net8.0", dll);
//            if (!File.Exists(targetFilePath)) throw new FileNotFoundException(targetFilePath);
//            File.Copy(targetFilePath, Path.Combine(AppContext.BaseDirectory, dll), true);
//        }
//#endif
}
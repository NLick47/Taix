using System;
using Avalonia;
using ReactiveUI.Avalonia;

namespace Taix.Client;

internal sealed class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        var builder = AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .UseReactiveUI(_ => { });

#if DEBUG
        builder.WithDeveloperTools();
#endif
        return builder;
    }
}

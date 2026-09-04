using System;
using Avalonia;

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
            // macOS 弹层默认是独立 NSPanel，选完下拉窗口会挪动且悬浮全部失效；
            // 改成在窗口内渲染弹层绕开。此选项只有 macOS 后端会读，Windows 不受影响
            .With(new AvaloniaNativePlatformOptions { OverlayPopups = true });

#if DEBUG
        builder.WithDeveloperTools();
#endif
        return builder;
    }
}

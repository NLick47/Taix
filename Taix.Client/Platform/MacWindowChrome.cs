using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Avalonia.Controls;
using Taix.Client.Logging;

namespace Taix.Client.Platform;

[SupportedOSPlatform("macos")]
internal static class MacWindowChrome
{
    private const string LibraryName = "TaixWindowKit";

    public const double WindowCornerRadius = 20;

    public const double TrafficLightScale = 1.15;

    [DllImport(LibraryName, EntryPoint = "taix_window_set_thick_titlebar")]
    private static extern double NativeSetThickTitlebar(IntPtr nsWindow, int thick);

    [DllImport(LibraryName, EntryPoint = "taix_window_set_corner_radius")]
    private static extern int NativeSetCornerRadius(IntPtr nsWindow, double radius);

    [DllImport(LibraryName, EntryPoint = "taix_window_set_traffic_light_scale")]
    private static extern int NativeSetTrafficLightScale(IntPtr nsWindow, double scale);

    public static bool SetTrafficLightScale(Window window, double scale)
    {
        if (!OperatingSystem.IsMacOS()) return false;

        try
        {
            var handle = window.TryGetPlatformHandle();
            if (handle is null || handle.Handle == IntPtr.Zero) return false;

            var result = NativeSetTrafficLightScale(handle.Handle, scale);
            if (result != 0)
            {
                Logger.Warn($"红绿灯缩放失败：原生返回 {result}");
                return false;
            }

            return true;
        }
        catch (DllNotFoundException)
        {
            Logger.Warn($"红绿灯缩放不可用：未找到 {LibraryName} 动态库");
            return false;
        }
        catch (Exception ex)
        {
            Logger.Error($"红绿灯缩放异常: {ex.Message}", ex);
            return false;
        }
    }

    public static bool SetCornerRadius(Window window, double radius)
    {
        if (!OperatingSystem.IsMacOS()) return false;

        try
        {
            var handle = window.TryGetPlatformHandle();
            if (handle is null || handle.Handle == IntPtr.Zero) return false;

            var result = NativeSetCornerRadius(handle.Handle, radius);
            if (result != 0)
            {
                Logger.Warn($"窗口圆角设置失败：原生返回 {result}");
                return false;
            }

            return true;
        }
        catch (DllNotFoundException)
        {
            Logger.Warn($"窗口圆角不可用：未找到 {LibraryName} 动态库");
            return false;
        }
        catch (Exception ex)
        {
            Logger.Error($"窗口圆角设置异常: {ex.Message}", ex);
            return false;
        }
    }

    public static double SetThickTitleBar(Window window, bool thick)
    {
        if (!OperatingSystem.IsMacOS()) return -1;

        try
        {
            var handle = window.TryGetPlatformHandle();
            if (handle is null || handle.Handle == IntPtr.Zero)
            {
                Logger.Warn("厚标题栏未启用：拿不到 NSWindow 句柄");
                return -1;
            }

            var height = NativeSetThickTitlebar(handle.Handle, thick ? 1 : 0);
            if (height <= 0)
            {
                Logger.Warn($"厚标题栏切换失败：原生返回 {height}");
                return height;
            }

            Logger.Info($"厚标题栏已{(thick ? "启用" : "停用")} height={height:0.#}");
            return height;
        }
        catch (DllNotFoundException)
        {
            Logger.Warn($"厚标题栏不可用：未找到 {LibraryName} 动态库");
            return -1;
        }
        catch (Exception ex)
        {
            Logger.Error($"厚标题栏切换异常: {ex.Message}", ex);
            return -1;
        }
    }
}

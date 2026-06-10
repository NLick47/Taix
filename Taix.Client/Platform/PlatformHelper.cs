using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Taix.Client.Platform;

public static class PlatformHelper
{
    public static double IconScaleFactor => RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? 1.25 : 1.0;
    public static double NavIconScaleFactor => RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? 1.2 : 1.0;
    public static bool IsMacOS => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
    public static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    public static void OpenFileInExplorer(string filePath)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            Process.Start("explorer.exe", $"/select,\"{filePath}\"");
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            Process.Start("open", $"-R \"{filePath}\"");
        else
            Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
    }

    public static void RunFile(string filePath)
    {
        Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
    }
}

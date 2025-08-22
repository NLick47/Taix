using System.Runtime.InteropServices;

namespace SharedLibrary.Librarys;

public static class PlatformInfo
{
    public static string GetPlatformName()
    {
        string[] platforms = new[] { "Win", "Mac", "Linux" };
        var platformName = string.Empty;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            platformName = platforms[0];
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            platformName = platforms[1];
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) platformName = platforms[2];
        return platformName;
    }
}
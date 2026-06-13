namespace Taix.Client.Platform;

public static class PlatformConstants
{
#if MACOS
    // macOS 平台图标大小
    public const double IconFontSize = 18;
    public const double ListChartIconSize = 27;
    public const double FrequentListIconSize = 22;
    public const double SmallListIconSize = 20;
    public const double InputBoxIconSize = 18;
#else
    public const double IconFontSize = 16;
    public const double ListChartIconSize = 25;
    public const double FrequentListIconSize = 20;
    public const double SmallListIconSize = 18;
    public const double InputBoxIconSize = 16;
#endif
}

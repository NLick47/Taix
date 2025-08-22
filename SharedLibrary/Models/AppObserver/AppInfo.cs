using SharedLibrary.Enums;

namespace SharedLibrary.Models.AppObserver;

/// <summary>
///     应用信息
/// </summary>
public class AppInfo
{
    public static AppInfo Empty = new(IntPtr.Zero, 0, string.Empty, string.Empty, string.Empty);

    public AppInfo(IntPtr windowHandle_, int pid_, string process_, string description_, string executablePath_,
        AppType type_ = AppType.Win32)
    {
        WindowHandle = windowHandle_;
        PID = pid_;
        Process = process_;
        Description = description_;
        ExecutablePath = executablePath_;
        Type = type_;
    }

    /// <summary>
    ///     进程ID
    /// </summary>
    public int PID { get; }

    /// <summary>
    ///     应用类型
    /// </summary>
    public AppType Type { get; }

    /// <summary>
    ///     进程名称
    /// </summary>
    public string Process { get; }

    /// <summary>
    ///     应用描述
    /// </summary>
    public string Description { get; }

    /// <summary>
    ///     可执行文件路径
    /// </summary>
    public string ExecutablePath { get; }

    /// <summary>
    ///     窗口句柄
    /// </summary>
    public IntPtr WindowHandle { get; }

    public override string ToString()
    {
        return
            $"WindowHandle:{WindowHandle},PID:{PID},ProcessName:{Process},Description:{Description},ExecutablePath:{ExecutablePath},Type:{Type}";
    }
}
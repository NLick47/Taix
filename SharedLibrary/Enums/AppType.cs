namespace SharedLibrary.Enums;

/// <summary>
///     应用类型
/// </summary>
public enum AppType
{
    /// <summary>
    ///     传统应用
    /// </summary>
    Win32,

    /// <summary>
    ///     UWP
    /// </summary>
    UWP,

    /// <summary>
    ///     系统组件，如任务栏、开始菜单、状态栏、系统登录界面等
    /// </summary>
    SystemComponent
}
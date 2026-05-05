using System;
using Taix.Client.Shared.Event;
using Taix.Client.Shared.Servicers.Interfaces;

namespace Taix.Client.Shared.Helpers;

/// <summary>
/// IAppConfig 扩展方法，提供细粒度的配置变更订阅能力，便于统一管理和释放订阅。
/// </summary>
public static class AppConfigExtensions
{
    /// <summary>
    /// 订阅指定配置路径的变更。
    /// </summary>
    public static IDisposable WhenChanged(this IAppConfig config, string path, Action handler)
    {
        EventHandler<ConfigChangedEventArgs> wrapper = (s, e) =>
        {
            if (e.HasChange(path))
                handler();
        };
        config.ConfigChanged += wrapper;
        return new DisposableAction(() => config.ConfigChanged -= wrapper);
    }

    /// <summary>
    /// 订阅语言配置变更。
    /// </summary>
    public static IDisposable WhenLanguageChanged(this IAppConfig config, Action handler)
        => config.WhenChanged("General.Language", handler);

    /// <summary>
    /// 订阅主题模式配置变更。
    /// </summary>
    public static IDisposable WhenThemeChanged(this IAppConfig config, Action handler)
        => config.WhenChanged("General.Theme", handler);

    /// <summary>
    /// 订阅主题颜色配置变更。
    /// </summary>
    public static IDisposable WhenThemeColorChanged(this IAppConfig config, Action handler)
        => config.WhenChanged("General.ThemeColor", handler);

    /// <summary>
    /// 订阅窗口渐变配置变更。
    /// </summary>
    public static IDisposable WhenWindowGradientChanged(this IAppConfig config, Action handler)
        => config.WhenChanged("General.IsWindowGradient", handler);

    /// <summary>
    /// 订阅主题相关配置变更（主题模式或主题颜色或窗口渐变）。
    /// </summary>
    public static IDisposable WhenAnyThemeRelatedChanged(this IAppConfig config, Action handler)
    {
        EventHandler<ConfigChangedEventArgs> wrapper = (s, e) =>
        {
            if (e.HasAnyChange("General.Theme", "General.ThemeColor", "General.IsWindowGradient"))
                handler();
        };
        config.ConfigChanged += wrapper;
        return new DisposableAction(() => config.ConfigChanged -= wrapper);
    }
}

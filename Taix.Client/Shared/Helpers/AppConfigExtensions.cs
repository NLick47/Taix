using System;
using Taix.Client.Shared.Event;
using Taix.Client.Shared.Servicers.Interfaces;

namespace Taix.Client.Shared.Helpers;

public static class AppConfigExtensions
{
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

    public static IDisposable WhenLanguageChanged(this IAppConfig config, Action handler)
        => config.WhenChanged("General.Language", handler);

    public static IDisposable WhenThemeChanged(this IAppConfig config, Action handler)
        => config.WhenChanged("General.Theme", handler);

    public static IDisposable WhenThemeColorChanged(this IAppConfig config, Action handler)
        => config.WhenChanged("General.ThemeColor", handler);

    public static IDisposable WhenWindowGradientChanged(this IAppConfig config, Action handler)
        => config.WhenChanged("General.WindowGradientScheme", handler);

    public static IDisposable WhenAnyThemeRelatedChanged(this IAppConfig config, Action handler)
    {
        EventHandler<ConfigChangedEventArgs> wrapper = (s, e) =>
        {
            if (e.HasAnyChange("General.Theme", "General.ThemeColor", "General.WindowGradientScheme"))
                handler();
        };
        config.ConfigChanged += wrapper;
        return new DisposableAction(() => config.ConfigChanged -= wrapper);
    }

    public static IDisposable WhenShortcutsChanged(this IAppConfig config, Action handler)
    {
        EventHandler<ConfigChangedEventArgs> wrapper = (s, e) =>
        {
            if (e.HasAnyChange("Shortcut.Refresh", "Shortcut.Search", "Shortcut.NavigateBack"))
                handler();
        };
        config.ConfigChanged += wrapper;
        return new DisposableAction(() => config.ConfigChanged -= wrapper);
    }
}

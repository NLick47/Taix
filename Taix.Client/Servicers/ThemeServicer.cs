using System;
using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using Taix.Client.Shared.Models.Config;
using Taix.Client.Shared.Servicers.Interfaces;
using Taix.Client.Views;
using Colors = Taix.Client.Base.Color.Colors;

namespace Taix.Client.Servicers;

public class ThemeServicer : IThemeServicer
{
    private readonly IAppConfig appConfig;
    private readonly MainWindow mainWindow;
    private readonly ThemeVariant[] themeOptions = { ThemeVariant.Default, ThemeVariant.Light, ThemeVariant.Dark };

    public ThemeServicer(IAppConfig appConfig, MainWindow main)
    {
        this.appConfig = appConfig;
        mainWindow = main;
        appConfig.ConfigChanged += AppConfig_ConfigChanged;
    }

    public event EventHandler OnThemeChanged;

    public void Init()
    {
        LoadTheme(themeOptions[appConfig.GetConfig().General.Theme]);
    }

    public void LoadTheme(ThemeVariant theme, bool isRefresh = false)
    {
        mainWindow.RequestedThemeVariant = theme;
        UpdateThemeColor();
    }

    public void SetMainWindow(MainWindow mainWindow)
    {
    }

    private void AppConfig_ConfigChanged(ConfigModel oldConfig, ConfigModel newConfig)
    {
        if (oldConfig.General.Theme != newConfig.General.Theme)
        {
            LoadTheme(themeOptions[newConfig.General.Theme]);
            OnThemeChanged?.Invoke(this, EventArgs.Empty);
        }

        if (oldConfig.General.ThemeColor != newConfig.General.ThemeColor)
        {
            LoadTheme(themeOptions[newConfig.General.Theme], true);
            OnThemeChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void UpdateThemeColor()
    {
        var config = appConfig.GetConfig();
        if (string.IsNullOrEmpty(config.General.ThemeColor))
        {
            StateData.ThemeColor = Application.Current.Resources["ThemeColor"]?.ToString() ?? "#FFFF1BBC";
            return;
        }

        StateData.ThemeColor = config.General.ThemeColor;
        Application.Current.Resources["ThemeColor"] = Color.Parse(config.General.ThemeColor);
        Application.Current.Resources["ThemeBrush"] = Base.Color.Colors.GetFromString(config.General.ThemeColor);
    }
}

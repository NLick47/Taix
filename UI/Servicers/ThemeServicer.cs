using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Core.Models.Config;
using Core.Servicers.Interfaces;
using ReactiveUI;
using UI.Views;
using Colors = UI.Base.Color.Colors;

namespace UI.Servicers;

public class ThemeServicer : IThemeServicer
{
    private readonly IAppConfig appConfig;
    private readonly MainWindow mainWindow;
    private readonly ThemeVariant[] themeOptions = { ThemeVariant.Default, ThemeVariant.Light, ThemeVariant.Dark };
    private IReadOnlyList<IResourceProvider> MergedDictionaries;

    /// <summary>
    ///     当前主题名称
    /// </summary>
    private string themeName;

    private IDisposable? windowSizeSubscription;


    public ThemeServicer(IAppConfig appConfig, MainWindow main)
    {
        this.appConfig = appConfig;
        mainWindow = main;
        appConfig.ConfigChanged += AppConfig_ConfigChanged;
    }

    public event EventHandler OnThemeChanged;


    public void Init()
    {
        var config = appConfig.GetConfig();
        LoadTheme(themeOptions[appConfig.GetConfig().General.Theme]);
        HandleWindowSizeChangedEvent();
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

        if (oldConfig.General.IsSaveWindowSize != newConfig.General.IsSaveWindowSize) HandleWindowSizeChangedEvent();
    }

    private void HandleWindowSizeChangedEvent()
    {
        if (mainWindow == null || mainWindow.IsWindowClosed) return;

        var config = appConfig.GetConfig();

        windowSizeSubscription?.Dispose();
        windowSizeSubscription = null;
        if (config.General.IsSaveWindowSize)
            windowSizeSubscription = mainWindow.WhenAnyValue(x => x.Width, x => x.Height)
                .Throttle(TimeSpan.FromMilliseconds(1000 * 3))
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(tuple =>
                {
                    var (width, height) = tuple;
                    var config = appConfig.GetConfig();
                    if (config.General.IsSaveWindowSize)
                    {
                        config.General.WindowWidth = width;
                        config.General.WindowHeight = height;
                        appConfig.Save();
                    }
                });
    }

    private void UpdateThemeColor()
    {
        var config = appConfig.GetConfig();
        if (string.IsNullOrEmpty(config.General.ThemeColor))
        {
            StateData.ThemeColor = Application.Current.Resources["ThemeColor"].ToString();
            return;
        }

        StateData.ThemeColor = config.General.ThemeColor;
        Application.Current.Resources["ThemeColor"] = Color.Parse(config.General.ThemeColor);
        Application.Current.Resources["ThemeBrush"] = Colors.GetFromString(config.General.ThemeColor);
    }
}
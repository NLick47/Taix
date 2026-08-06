using System;
using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using Taix.Client.Shared.Event;
using Taix.Client.Shared.Models.Config;
using Taix.Client.Shared.Servicers.Interfaces;
using Colors = Taix.Client.Base.Color.Colors;

namespace Taix.Client.Servicers;

public class ThemeServicer : IThemeServicer
{

    public static readonly ThemeVariant Azure = new("Azure", ThemeVariant.Light);

    private sealed record ThemeDefinition(
        ThemeVariant Variant,
        string? LockedAccent,
        Color? SolidBackground);

    private static readonly ThemeDefinition[] Definitions =
    {
        new(ThemeVariant.Default, null, null),
        new(ThemeVariant.Light, null, null),
        new(ThemeVariant.Dark, null, null),
        new(Azure, "#2F9BFF", Color.Parse("#FF4FA8E8")),
    };

    private static readonly Color LightSolid = Color.Parse("#ededf0");
    private static readonly Color DarkSolid = Color.Parse("#131315");

    private readonly IAppConfig _appConfig;

    public ThemeServicer(IAppConfig appConfig)
    {
        _appConfig = appConfig;
        _appConfig.ConfigChanged += OnConfigChanged;
    }

    public void Init()
    {
        LoadTheme(GetTheme(_appConfig.GetConfig().General.Theme));
    }

    public void LoadTheme(AppTheme theme)
    {
        var definition = Definitions[(int)theme];

        void Apply()
        {
            if (Application.Current == null) return;
            ApplyTheme(definition);
        }

        if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            Apply();
        }
        else
        {
            Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(Apply);
        }
    }

    public void SetMainWindow(Views.MainWindow mainWindow)
    {
    }

    private void OnConfigChanged(object? sender, ConfigChangedEventArgs e)
    {
        if (e.HasChange("General.Theme") ||
            e.HasChange("General.ThemeColor") ||
            e.HasChange("General.WindowGradientScheme"))
        {
            LoadTheme(GetTheme(e.NewConfig.General.Theme));
        }
    }

    private void ApplyTheme(ThemeDefinition definition)
    {
        var app = Application.Current!;
        var config = _appConfig.GetConfig();

        // 切换变体，ThemeDictionaries 自动选对应字典
        app.RequestedThemeVariant = definition.Variant;

        // 主题色
        var accent = definition.LockedAccent ?? config.General.ThemeColor;
        if (string.IsNullOrEmpty(accent))
        {
            accent = app.Resources.TryGetResource("ThemeColor", definition.Variant, out var value)
                ? value?.ToString()
                : "#FFFF1BBC";
        }
        StateData.ThemeColor = accent!;
        app.Resources["ThemeColor"] = Color.Parse(accent!);
        app.Resources["ThemeBrush"] = Colors.GetFromString(accent!);

        // 窗口背景与边框
        if (definition.SolidBackground is { } solid)
        {
            app.Resources["WindowBackground"] = new SolidColorBrush(solid);
            return;
        }

        var isLight = definition.Variant == ThemeVariant.Light ||
                      (definition.Variant == ThemeVariant.Default && app.ActualThemeVariant != ThemeVariant.Dark);

        var gradientKey = config.General.WindowGradientScheme switch
        {
            1 => "WindowBackgroundModern",
            2 => "WindowBackgroundClassic",
            3 => "WindowBackgroundOriginal",
            _ => null,
        };

        if (gradientKey != null &&
            app.Resources.TryGetResource(gradientKey, definition.Variant, out var gradient) &&
            gradient is IBrush gradientBrush)
        {
            app.Resources["WindowBackground"] = gradientBrush;
        }
        else
        {
            app.Resources["WindowBackground"] = new SolidColorBrush(isLight ? LightSolid : DarkSolid);
        }
    }

    private static AppTheme GetTheme(int index) =>
        index >= 0 && index < Definitions.Length ? (AppTheme)index : AppTheme.System;
}

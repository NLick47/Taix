using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using Taix.Client.Shared.Event;
using Taix.Client.Shared.Servicers.Interfaces;
using Taix.Client.Views;
using Colors = Taix.Client.Base.Color.Colors;

namespace Taix.Client.Servicers;

public class ThemeServicer : IThemeServicer
{
    private readonly IAppConfig _appConfig;
    private readonly ThemeVariant[] _themeOptions = { ThemeVariant.Default, ThemeVariant.Light, ThemeVariant.Dark };

    public ThemeServicer(IAppConfig appConfig)
    {
        _appConfig = appConfig;
        _appConfig.ConfigChanged += OnConfigChanged;
    }

    public void Init()
    {
        LoadTheme(_themeOptions[_appConfig.GetConfig().General.Theme]);
        UpdateWindowBackground();
    }

    public void LoadTheme(ThemeVariant theme, bool isRefresh = false)
    {
        Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (Application.Current != null)
                Application.Current.RequestedThemeVariant = theme;
            UpdateThemeColor();
            UpdateWindowBackground();
        });
    }

    public void SetMainWindow(MainWindow mainWindow)
    {
    }

    private void OnConfigChanged(object? sender, ConfigChangedEventArgs e)
    {
        if (e.HasChange("General.Theme"))
        {
            LoadTheme(_themeOptions[e.NewConfig.General.Theme]);
        }

        if (e.HasChange("General.ThemeColor"))
        {
            LoadTheme(_themeOptions[e.NewConfig.General.Theme], true);
        }

        if (e.HasChange("General.IsWindowGradient"))
        {
            UpdateWindowBackground();
        }
    }

    private void UpdateThemeColor()
    {
        var config = _appConfig.GetConfig();
        if (string.IsNullOrEmpty(config.General.ThemeColor))
        {
            StateData.ThemeColor = Application.Current?.Resources["ThemeColor"]?.ToString() ?? "#FFFF1BBC";
            return;
        }

        StateData.ThemeColor = config.General.ThemeColor;
        if (Application.Current != null)
        {
            Application.Current.Resources["ThemeColor"] = Color.Parse(config.General.ThemeColor);
            Application.Current.Resources["ThemeBrush"] = Colors.GetFromString(config.General.ThemeColor);
        }
    }

    private void UpdateWindowBackground()
    {
        var config = _appConfig.GetConfig();
        if (Application.Current == null) return;

        if (config.General.IsWindowGradient)
        {
            Application.Current.Resources.Remove("WindowBackground");
        }
        else
        {
            var isLight = Application.Current.ActualThemeVariant == ThemeVariant.Light;
            var color = isLight ? Color.Parse("#ededf0") : Color.Parse("#131315");
            Application.Current.Resources["WindowBackground"] = new SolidColorBrush(color);
        }
    }
}

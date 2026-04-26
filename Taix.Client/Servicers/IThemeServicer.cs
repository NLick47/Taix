using System;
using Avalonia.Styling;
using Taix.Client.Views;

namespace Taix.Client.Servicers;

public interface IThemeServicer
{
    void Init();
    void LoadTheme(ThemeVariant theme, bool isRefresh = false);
    void SetMainWindow(MainWindow mainWindow);

    /// <summary>
    ///     切换主题时发生
    /// </summary>
    event EventHandler OnThemeChanged;
}
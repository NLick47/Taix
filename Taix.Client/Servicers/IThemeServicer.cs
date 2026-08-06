using Taix.Client.Shared.Models.Config;
using Taix.Client.Views;

namespace Taix.Client.Servicers;

public interface IThemeServicer
{
    void Init();
    void LoadTheme(AppTheme theme);
    void SetMainWindow(MainWindow mainWindow);
}

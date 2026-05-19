using Avalonia;
using Avalonia.Controls;
using Taix.Client.Controls.Window;

namespace Taix.Client.Views;

public partial class MainWindow : DefaultWindow
{
    public MainWindow()
    {
        InitializeComponent();
        RequestClose += (_, _) => Close();

        if (!UseCustomWindowChrome)
        {
            NavigationHost.Margin = new Thickness(0, 0, 0, 15);
            PageHost.Margin = new Thickness(0, 0, 10, 10);
        }
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);

        if (App.IsShuttingDown) return;
        if (e.Cancel) return;

        e.Cancel = true;
        _ = App.ExitAsync();
    }
}

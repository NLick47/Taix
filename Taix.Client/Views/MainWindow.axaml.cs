using Avalonia.Controls;
using Taix.Client.Controls.Window;

namespace Taix.Client.Views;

public partial class MainWindow : DefaultWindow
{
    public MainWindow()
    {
        InitializeComponent();
        RequestClose += (_, _) => Close();
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

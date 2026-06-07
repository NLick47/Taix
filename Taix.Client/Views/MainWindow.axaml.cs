using System;
using Avalonia.Controls;
using Taix.Client.Controls.Window;

namespace Taix.Client.Views;

public partial class MainWindow : DefaultWindow
{
    public MainWindow()
    {
        InitializeComponent();
        RequestClose += (_, _) => Close();

        if (OperatingSystem.IsMacOS())
        {
            ExtendClientAreaToDecorationsHint = true;
            ExtendClientAreaTitleBarHeightHint = -1;
            WindowDecorations = WindowDecorations.Full;
        }
    }

    protected override Type StyleKeyOverride =>
        OperatingSystem.IsMacOS() ? typeof(MacOSWindow) : typeof(DefaultWindow);

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);

        if (App.IsShuttingDown) return;
        if (e.Cancel) return;

        e.Cancel = true;
        _ = App.ExitAsync();
    }
}
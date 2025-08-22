using System;
using System.Diagnostics;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace UI;

public partial class ErrorDialog : Window
{
    public ErrorDialog()
    {
        InitializeComponent();
    }

    private void LogHyperlinkClick(object sender, RoutedEventArgs e)
    {
        var loggerName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
            "Log", DateTime.Now.ToString("yyyy-MM-dd") + ".log");
        if (File.Exists(loggerName)) Process.Start("explorer.exe", "/select, " + loggerName);
    }

    private void IssuesHyperlinkClick(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo("https://github.com/NLick47/Taix/issues/new")
                { UseShellExecute = true });
        }
        catch (Exception)
        {
        }
    }

    private void Button_Click(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo("https://github.com/NLick47/Taix") { UseShellExecute = true });
        Close();
    }
}
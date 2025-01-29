using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System;
using System.IO;

namespace UI;

public partial class ErrorDialog : Window
{
    public ErrorDialog()
    {
        InitializeComponent();
    }

    private void LogHyperlinkClick(object sender, RoutedEventArgs e)
    {
        string loggerName = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                     "Log", DateTime.Now.ToString("yyyy-MM-dd") + ".log");
        if (File.Exists(loggerName))
        {
            System.Diagnostics.Process.Start("explorer.exe", "/select, " + loggerName);
        }
    }

    private void IssuesHyperlinkClick(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo("https://github.com/NLick47/Taix/issues/new") { UseShellExecute = true });
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
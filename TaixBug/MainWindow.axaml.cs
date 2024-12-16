using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace TaixBug
{
    public partial class MainWindow : Window
    {
        public MainWindow()
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

        private void Restart(object sender, RoutedEventArgs e)
        {
            var p = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "Taix.exe" : "Taix";
            string taiPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
               p);
            if (File.Exists(taiPath))
            {
                Process.Start(taiPath);
                Close();
            }
            else
            {
                this.Popup.IsOpen = true;

            }

        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo("https://github.com/NLick47/Taix") { UseShellExecute = true });
            Close();
        }
    }
}
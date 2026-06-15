using System.Diagnostics;
using System.IO;
using Taix.Client.Platform;
using Taix.Client.Servicers.Interfaces;

namespace Taix.Client.Servicers;

public class ProcessService : IProcessService
{
    public void OpenUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        if (!url.Contains("://")) url = "http://" + url;
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    public void OpenFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return;
        Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
    }

    public void OpenDirectory(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return;
        PlatformHelper.OpenFileInExplorer(filePath);
    }
}

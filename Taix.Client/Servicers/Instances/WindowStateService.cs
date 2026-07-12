using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Taix.Client.Shared.Models.Config;
using Taix.Client.Shared.Servicers.Interfaces;

namespace Taix.Client.Servicers.Instances;

public class WindowStateService : IWindowStateService
{
    public double WindowWidth { get; set; }
    public double WindowHeight { get; set; }
    public bool IsMaximized { get; set; }

    private static string CacheFilePath
    {
        get
        {
            if (OperatingSystem.IsMacOS())
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Taix",
                    "window.cache.json");
            }

            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "window.cache.json");
        }
    }

    public WindowStateService()
    {
    }

    public Task LoadAsync()
    {
        try
        {
            if (File.Exists(CacheFilePath))
            {
                var json = File.ReadAllText(CacheFilePath);
                var state = JsonSerializer.Deserialize(json, ClientJsonContext.Default.WindowStateModel);
                if (state != null)
                {
                    WindowWidth = state.WindowWidth;
                    WindowHeight = state.WindowHeight;
                    IsMaximized = state.IsMaximized;
                }
            }
        }
        catch
        {
            // Ignore load errors, use defaults
        }

        return Task.CompletedTask;
    }

    public Task SaveAsync()
    {
        try
        {
            var state = new WindowStateModel
            {
                WindowWidth = WindowWidth,
                WindowHeight = WindowHeight,
                IsMaximized = IsMaximized
            };
            var json = JsonSerializer.Serialize(state, ClientJsonContext.Default.WindowStateModel);

            var directory = Path.GetDirectoryName(CacheFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(CacheFilePath, json);
        }
        catch
        {
            // Ignore save errors
        }

        return Task.CompletedTask;
    }
}

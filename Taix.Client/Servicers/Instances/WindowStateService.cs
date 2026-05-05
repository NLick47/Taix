using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Taix.Client.Shared.Models.Config;
using Taix.Client.Shared.Servicers.Interfaces;

namespace Taix.Client.Servicers.Instances;

public class WindowStateService : IWindowStateService
{
    private readonly string _cacheFilePath;

    public double WindowWidth { get; set; }
    public double WindowHeight { get; set; }

    public WindowStateService()
    {
        _cacheFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "window.cache.json");
    }

    public Task LoadAsync()
    {
        try
        {
            if (File.Exists(_cacheFilePath))
            {
                var json = File.ReadAllText(_cacheFilePath);
                var state = JsonSerializer.Deserialize(json, ClientJsonContext.Default.WindowStateModel);
                if (state != null)
                {
                    WindowWidth = state.WindowWidth;
                    WindowHeight = state.WindowHeight;
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
                WindowHeight = WindowHeight
            };
            var json = JsonSerializer.Serialize(state, ClientJsonContext.Default.WindowStateModel);
            File.WriteAllText(_cacheFilePath, json);
        }
        catch
        {
            // Ignore save errors
        }

        return Task.CompletedTask;
    }
}

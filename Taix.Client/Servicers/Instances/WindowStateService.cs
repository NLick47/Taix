using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Taix.Client.Shared.Models.Config;
using Taix.Client.Shared.Servicers.Interfaces;

namespace Taix.Client.Servicers.Instances;

public class WindowStateService : IWindowStateService
{
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

    public WindowSnapshot? Last { get; private set; }

    public Task LoadAsync()
    {
        try
        {
            if (File.Exists(CacheFilePath))
            {
                var json = File.ReadAllText(CacheFilePath);
                var state = JsonSerializer.Deserialize(json, ClientJsonContext.Default.WindowStateModel);
                Last = Read(state);
            }
        }
        catch
        {
            // 缓存损坏时退回默认值，不应该影响启动
            Last = null;
        }

        return Task.CompletedTask;
    }

    public Task SaveAsync(WindowSnapshot snapshot)
    {
        Last = snapshot;

        try
        {
            var state = new WindowStateModel
            {
                HasValue = true,
                State = snapshot.State,
                X = snapshot.X,
                Y = snapshot.Y,
                Width = snapshot.Width,
                Height = snapshot.Height
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

    private static WindowSnapshot? Read(WindowStateModel? state)
    {
        if (state == null) return null;

        var snapshot = state.HasValue
            ? new WindowSnapshot(state.X, state.Y, state.Width, state.Height, state.State)
            : Migrate(state);

        return snapshot.IsValid ? snapshot : null;
    }

    /// <summary>
    /// 迁移旧版本缓存
    /// </summary>
    private static WindowSnapshot Migrate(WindowStateModel state)
    {
        var kind = state.IsMaximized == true ? WindowStateKind.Maximized : WindowStateKind.Normal;
        return new WindowSnapshot(null, null, state.WindowWidth, state.WindowHeight, kind);
    }
}

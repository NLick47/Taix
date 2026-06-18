using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Threading;
using Taix.Client.Librarys.Api;
using Taix.Client.Logging;
using Taix.Client.Shared.Event;
using Taix.Client.Shared.Models.Config;
using Taix.Client.Shared.Servicers.Interfaces;

namespace Taix.Client.Servicers.Instances;

public class ApiAppConfig : IAppConfig
{
    private readonly HttpClient _httpClient;
    private ConfigModel? _config;
    private ConfigModel? _oldConfig;

    /// <summary>
    /// 已知的服务器配置快照。用于防止后台刷新覆盖用户未保存的本地修改。
    /// </summary>
    private ConfigModel? _serverSnapshot;

    private static string CacheFilePath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.config.cache.json");

    public ApiAppConfig(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public event EventHandler<ConfigChangedEventArgs>? ConfigChanged;

    public Task LoadAsync()
    {
        // 1. 先尝试从本地缓存加载（同步，瞬间完成）
        var cachedConfig = LoadFromCache();

        if (cachedConfig != null)
        {
            // 有缓存：立即应用缓存配置，窗口可以秒开；然后后台刷新远程配置
            // 此分支完全同步，直接返回 Task.CompletedTask，避免 async 状态机开销
            ApplyConfig(cachedConfig);
            _serverSnapshot = CloneConfig(cachedConfig);
            _ = RefreshFromServerAsync();
            return Task.CompletedTask;
        }

        // 无缓存：立即应用默认配置避免阻塞启动，后台异步加载远程配置
        var fallback = new ConfigModel
        {
            General = new GeneralModel(),
            Behavior = new BehaviorModel()
        };
        ApplyConfig(fallback);
        _ = LoadFromRemoteAndApplyAsync();
        return Task.CompletedTask;
    }

    private async Task LoadFromRemoteAndApplyAsync()
    {
        try
        {
            var remoteConfig = await LoadFromServerAsync();
            if (remoteConfig != null)
            {
                ApplyConfig(remoteConfig);
                SaveToCache(remoteConfig);
                _serverSnapshot = CloneConfig(remoteConfig);
            }
        }
        catch (Exception ex)
        {
            Logger.Warn($"后台加载远程配置失败，将使用默认配置: {ex.Message}");
        }
    }

    private async Task<ConfigModel?> LoadFromServerAsync()
    {
        try
        {
            return (await _httpClient.GetFromJsonAsync(
                "api/config",
                TaixApiJsonContext.Default.ApiResponseConfigModel))?.Data;
        }

        catch (Exception ex)
        {
            Logger.Error($"加载应用配置失败: {ex.Message}", ex);
        }
        return null;
    }

    private async Task RefreshFromServerAsync()
    {
        try
        {
            var serverConfig = await LoadFromServerAsync();
            if (serverConfig == null) return;

            if (_serverSnapshot != null && AreConfigsEqual(_serverSnapshot, serverConfig))
                return;

            if (_config != null && _serverSnapshot != null && !AreConfigsEqual(_config, _serverSnapshot))
            {
                _serverSnapshot = CloneConfig(serverConfig);
                SaveToCache(serverConfig);
                Logger.Info("检测到服务器配置变更，但本地有未保存的修改，已更新缓存文件，将在下次启动时生效");
                return;
            }

            ApplyConfig(serverConfig);
            _serverSnapshot = CloneConfig(serverConfig);
            SaveToCache(serverConfig);
        }
        catch (Exception ex)
        {
            Logger.Warn($"从服务器刷新配置失败，将使用缓存配置: {ex.Message}");
        }
    }

    private static ConfigModel? LoadFromCache()
    {
        try
        {
            if (File.Exists(CacheFilePath))
            {
                var json = File.ReadAllText(CacheFilePath);
                var config = JsonSerializer.Deserialize(json, ClientJsonContext.Default.ConfigModel);
                if (config != null)
                {
                    // 版本校验：缓存配置版本必须和当前模型版本一致
                    if (config.Version == ConfigModel.CurrentVersion)
                    {
                        return config;
                    }
                    Logger.Warn($"配置缓存版本不匹配（缓存: {config.Version}, 期望: {ConfigModel.CurrentVersion}），将重新加载");
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Warn($"读取配置缓存失败: {ex.Message}");
        }
        return null;
    }

    private static void SaveToCache(ConfigModel config)
    {
        try
        {
            var json = JsonSerializer.Serialize(config, ClientJsonContext.Default.ConfigModel);
            var tempPath = CacheFilePath + ".tmp";
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, CacheFilePath, overwrite: true);
        }
        catch (Exception ex)
        {
            Logger.Warn($"保存配置缓存失败: {ex.Message}");
        }
    }


    private static ConfigModel? CloneConfig(ConfigModel? source)
    {
        if (source == null) return null;
        var json = JsonSerializer.Serialize(source, TaixApiJsonContext.Default.ConfigModel);
        return JsonSerializer.Deserialize(json, TaixApiJsonContext.Default.ConfigModel);
    }

    private void ApplyConfig(ConfigModel config)
    {
        var previousConfig = _config;
        _config = config;

        ConfigChangedEventArgs? args = null;

        if (previousConfig is null)
        {
            var emptyConfig = new ConfigModel
            {
                General = new GeneralModel(),
                Behavior = new BehaviorModel()
            };
            var changes = DetectChanges(emptyConfig, config);
            args = new ConfigChangedEventArgs(emptyConfig, config, changes);
        }
        else if (!AreConfigsEqual(previousConfig, config))
        {
            var changes = DetectChanges(previousConfig, config);
            args = new ConfigChangedEventArgs(previousConfig, _config, changes);
        }

        if (args != null)
        {
            RaiseConfigChanged(args);
        }

        CopyToOldConfig();
    }

    private static bool AreConfigsEqual(ConfigModel a, ConfigModel b)
    {
        var jsonA = JsonSerializer.Serialize(a, TaixApiJsonContext.Default.ConfigModel);
        var jsonB = JsonSerializer.Serialize(b, TaixApiJsonContext.Default.ConfigModel);
        return jsonA == jsonB;
    }

    public ConfigModel GetConfig()
    {
        if (_config is null)
        {
            return new ConfigModel
            {
                General = new GeneralModel(),
                Behavior = new BehaviorModel()
            };
        }

        return _config;
    }

    public async Task SaveAsync()
    {
        if (_config is null)
        {
            throw new InvalidOperationException("配置尚未加载，无法保存。");
        }

        try
        {
            using var response = await _httpClient.PostAsJsonAsync(
                "api/config",
                _config,
                TaixApiJsonContext.Default.ConfigModel);

            response.EnsureSuccessStatusCode();

            // 服务器保存成功后，立即同步本地缓存和服务器快照
            // 顺序：先确保服务器成功，再写本地缓存。即使写缓存崩溃，下次启动后台刷新会纠正
            SaveToCache(_config);
            _serverSnapshot = CloneConfig(_config);

            var changes = DetectChanges(_oldConfig ?? _config, _config);
            if (changes.Count > 0)
            {
                RaiseConfigChanged(new ConfigChangedEventArgs(_oldConfig ?? _config, _config, changes));
            }
            CopyToOldConfig();
        }
        catch (HttpRequestException ex)
        {
            Logger.Error($"保存配置到服务器失败: {ex.Message}", ex);
            throw new InvalidOperationException("无法将配置保存到服务器，请检查网络连接或服务器状态。", ex);
        }
        catch (TaskCanceledException ex)
        {
            Logger.Error($"保存配置到服务器超时: {ex.Message}", ex);
            throw new InvalidOperationException("保存配置到服务器时请求超时。", ex);
        }
    }

    private void RaiseConfigChanged(ConfigChangedEventArgs args)
    {
        if (ConfigChanged == null) return;

        if (Dispatcher.UIThread.CheckAccess())
        {
            ConfigChanged.Invoke(this, args);
        }
        else
        {
            Dispatcher.UIThread.InvokeAsync(() => ConfigChanged.Invoke(this, args));
        }
    }

    private static IReadOnlyList<string> DetectChanges(ConfigModel old, ConfigModel next)
    {
        var changes = new List<string>();

        if (old.General.Language != next.General.Language)
            changes.Add("General.Language");
        if (old.General.Theme != next.General.Theme)
            changes.Add("General.Theme");
        if (old.General.ThemeColor != next.General.ThemeColor)
            changes.Add("General.ThemeColor");
        if (old.General.IsWindowGradient != next.General.IsWindowGradient)
            changes.Add("General.IsWindowGradient");
        if (old.General.StartPage != next.General.StartPage)
            changes.Add("General.StartPage");
        if (old.General.IsAutoUpdate != next.General.IsAutoUpdate)
            changes.Add("General.IsAutoUpdate");
        if (old.General.IsWebEnabled != next.General.IsWebEnabled)
            changes.Add("General.IsWebEnabled");
        if (old.General.IsEnableTray != next.General.IsEnableTray)
            changes.Add("General.IsEnableTray");
        if (old.General.IsSaveWindowSize != next.General.IsSaveWindowSize)
            changes.Add("General.IsSaveWindowSize");
        if (old.General.DataRetentionDays != next.General.DataRetentionDays)
            changes.Add("General.DataRetentionDays");

        return changes;
    }

    private void CopyToOldConfig()
    {
        if (_config is null)
        {
            return;
        }

        var json = JsonSerializer.Serialize(_config, TaixApiJsonContext.Default.ConfigModel);
        _oldConfig = JsonSerializer.Deserialize(json, TaixApiJsonContext.Default.ConfigModel);
    }
}

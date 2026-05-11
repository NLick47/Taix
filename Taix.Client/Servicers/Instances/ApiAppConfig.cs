using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Threading;
using Taix.Client;
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

    public ApiAppConfig(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("TaixApi");
    }

    public event EventHandler<ConfigChangedEventArgs>? ConfigChanged;

    public async Task LoadAsync()
    {
        ConfigModel? config = null;
        try
        {
            config = (await _httpClient.GetFromJsonAsync(
                "api/config",
                TaixApiJsonContext.Default.ApiResponseConfigModel))?.Data;
        }
        catch (HttpRequestException ex)
        {
            Logger.Error($"无法从服务器加载应用配置，请检查网络连接或服务器状态。: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex)
        {
            Logger.Error($"从服务器加载应用配置时请求超时。: {ex.Message}", ex);
        }
        catch (JsonException ex)
        {
            Logger.Error($"从服务器返回的配置数据格式无效，无法解析。: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            Logger.Error($"加载应用配置失败: {ex.Message}", ex);
        }

        if (config is null)
        {
            config = new ConfigModel
            {
                General = new GeneralModel(),
                Behavior = new BehaviorModel(),
                Links = new System.Collections.Generic.List<Taix.Client.Shared.Models.Config.Link.LinkModel>()
            };
        }

        ApplyConfig(config);
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
                Behavior = new BehaviorModel(),
                Links = new System.Collections.Generic.List<Taix.Client.Shared.Models.Config.Link.LinkModel>()
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
                Behavior = new BehaviorModel(),
                Links = new System.Collections.Generic.List<Taix.Client.Shared.Models.Config.Link.LinkModel>()
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

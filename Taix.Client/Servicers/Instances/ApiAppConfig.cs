using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
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

    public event AppConfigEventHandler? ConfigChanged;

    public async Task LoadAsync()
    {
        ConfigModel? config;
        try
        {
            config =  (await _httpClient.GetFromJsonAsync(
                "api/config",
                TaixApiJsonContext.Default.ApiResponseConfigModel))?.Data;
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException("无法从服务器加载应用配置，请检查网络连接或服务器状态。", ex);
        }
        catch (TaskCanceledException ex)
        {
            throw new InvalidOperationException("从服务器加载应用配置时请求超时。", ex);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("从服务器返回的配置数据格式无效，无法解析。", ex);
        }

        if (config is null)
        {
            throw new InvalidOperationException("服务器返回的配置数据为空。");
        }

        var previousConfig = _config;
        _config = config;
        CopyToOldConfig();

        if (previousConfig is not null && !AreConfigsEqual(previousConfig, config))
        {
            ConfigChanged?.Invoke(previousConfig, _config);
        }
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
            var response = await _httpClient.PostAsJsonAsync(
                "api/config",
                _config,
                TaixApiJsonContext.Default.ConfigModel);

            response.EnsureSuccessStatusCode();

            ConfigChanged?.Invoke(_oldConfig ?? _config, _config);
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

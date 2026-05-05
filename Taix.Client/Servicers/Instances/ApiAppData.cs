using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Taix.Client.Librarys.Api;
using Taix.Client.Shared.Models;
using Taix.Client.Shared.Servicers.Interfaces;

namespace Taix.Client.Servicers.Instances;

public class ApiAppData : IAppData
{
    private readonly ITaixApiClient _apiClient;

    public ApiAppData(ITaixApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public Task<AppModel?> GetAppAsync(int id, CancellationToken cancellationToken = default)
    {
        return _apiClient.GetAppAsync(id);
    }

    public Task<AppModel?> GetAppByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return _apiClient.GetAppByNameAsync(name);
    }

    public async Task<IReadOnlyCollection<AppModel>> GetAllAppsAsync(CancellationToken cancellationToken = default)
    {
        var apps = await _apiClient.GetAllAppsAsync();
        return apps;
    }

    public async Task<IReadOnlyCollection<AppModel>> GetAppsByCategoryIDAsync(int categoryID, CancellationToken cancellationToken = default)
    {
        var apps = await _apiClient.GetAppsByCategoryAsync(categoryID);
        return apps;
    }

    public async Task<int> GetAppCountByCategoryIDAsync(int categoryID)
    {
        var apps = await _apiClient.GetAppsByCategoryAsync(categoryID);
        return apps.Count;
    }

    public async Task AddAppAsync(AppModel app, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(app);
        await _apiClient.CreateAppAsync(app);
    }

    public async Task UpdateAppAsync(AppModel app, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(app);
        await _apiClient.UpdateAppAsync(app);
    }
}

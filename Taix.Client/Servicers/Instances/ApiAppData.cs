using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Taix.Client.Librarys.Api;
using Taix.Client.Shared.Models;
using Taix.Client.Shared.Servicers.Interfaces;

namespace Taix.Client.Servicers.Instances;

public class ApiAppData : IAppData
{
    private readonly ITaixApiClient _apiClient;
    private readonly ConcurrentDictionary<int, AppModel> _appsById;
    private readonly ConcurrentDictionary<string, AppModel> _appsByName;

    public ApiAppData(ITaixApiClient apiClient)
    {
        _apiClient = apiClient;
        _appsById = new ConcurrentDictionary<int, AppModel>();
        _appsByName = new ConcurrentDictionary<string, AppModel>();
    }

    public void UpdateApp(AppModel app)
    {
        _apiClient.UpdateAppAsync(app).Wait();
        if (_appsById.ContainsKey(app.ID))
        {
            _appsById[app.ID] = app;
            _appsByName[app.Name!] = app;
        }
    }

    public AppModel GetApp(string name)
    {
        _appsByName.TryGetValue(name, out var app);
        return app;
    }

    public AppModel GetApp(int id)
    {
        _appsById.TryGetValue(id, out var app);
        return app;
    }

    public List<AppModel> GetAllApps() => _appsById.Values.ToList();

    public void AddApp(AppModel app)
    {
        var created = _apiClient.CreateAppAsync(app).Result;
        app.ID = created.ID;
        _appsById[app.ID] = app;
        _appsByName[app.Name!] = app;
    }

    public async Task LoadAsync()
    {
        var apps = await _apiClient.GetAllAppsAsync();
        _appsById.Clear();
        _appsByName.Clear();
        foreach (var app in apps)
        {
            _appsById[app.ID] = app;
            if (app.Name != null)
                _appsByName[app.Name] = app;
        }
    }

    public List<AppModel> GetAppsByCategoryID(int categoryID) =>
        _appsById.Values.Where(m => m.CategoryID == categoryID).ToList();
}

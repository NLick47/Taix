using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Taix.Client.Events;
using Taix.Client.Servicers.Interfaces;
using Taix.Client.Shared.Models;
using Taix.Client.Shared.Servicers.Interfaces;

namespace Taix.Client.Servicers;

public class AppUpdateService : IAppUpdateService
{
    private readonly IAppData _appData;
    private readonly IAppEventService _eventService;
    private readonly ICategorys _categoryService;

    public AppUpdateService(IAppData appData, IAppEventService eventService, ICategorys categoryService)
    {
        _appData = appData;
        _eventService = eventService;
        _categoryService = categoryService;
    }

    public async Task UpdateCategoryAsync(int appId, int categoryId)
    {
        var app = await _appData.GetAppAsync(appId);
        if (app == null) return;

        CategoryModel? category;
        if (categoryId > 0)
        {
            category = await _categoryService.GetCategoryAsync(categoryId);
        }
        else
        {
            var categories = await _categoryService.GetCategoriesAsync();
            category = categories.FirstOrDefault(c => c.IsSystem);
            categoryId = category?.ID ?? 0;
        }

        var updated = app with { CategoryID = categoryId, Category = category };
        await _appData.UpdateAppAsync(updated);

        _eventService.PublishAppChanged(updated, AppChangeType.Category);
    }

    public async Task ClearCategoryAsync(int appId)
    {
        var app = await _appData.GetAppAsync(appId);
        if (app == null) return;

        var categories = await _categoryService.GetCategoriesAsync();
        var systemCategory = categories.FirstOrDefault(c => c.IsSystem);

        var updated = app with { CategoryID = systemCategory?.ID ?? 0, Category = systemCategory };
        await _appData.UpdateAppAsync(updated);

        _eventService.PublishAppChanged(updated, AppChangeType.Category);
    }

    public async Task UpdateAliasAsync(int appId, string? alias)
    {
        var app = await _appData.GetAppAsync(appId);
        if (app == null) return;

        var updated = app with { Alias = alias };
        await _appData.UpdateAppAsync(updated);

        _eventService.PublishAppChanged(updated, AppChangeType.Alias);
    }

    public async Task UpdateDescriptionAsync(int appId, string? description)
    {
        var app = await _appData.GetAppAsync(appId);
        if (app == null) return;

        var updated = app with { Description = description };
        await _appData.UpdateAppAsync(updated);

        _eventService.PublishAppChanged(updated, AppChangeType.Description);
    }

    public async Task UpdateCategoryBatchAsync(IEnumerable<int> appIds, int categoryId)
    {
        CategoryModel? category;
        if (categoryId > 0)
        {
            category = await _categoryService.GetCategoryAsync(categoryId);
        }
        else
        {
            var categories = await _categoryService.GetCategoriesAsync();
            category = categories.FirstOrDefault(c => c.IsSystem);
            categoryId = category?.ID ?? 0;
        }

        var updatedApps = new List<AppModel>();
        foreach (var appId in appIds)
        {
            var app = await _appData.GetAppAsync(appId);
            if (app == null) continue;

            var updated = app with { CategoryID = categoryId, Category = category };
            await _appData.UpdateAppAsync(updated);
            updatedApps.Add(updated);
        }

        foreach (var app in updatedApps)
        {
            _eventService.PublishAppChanged(app, AppChangeType.Category);
        }
    }
}
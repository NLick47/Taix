using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Taix.Client.Librarys.Api;
using Taix.Client.Shared.Models;
using Taix.Client.Shared.Servicers.Interfaces;

namespace Taix.Client.Servicers.Instances;

public class ApiCategorys : ICategorys
{
    private readonly ITaixApiClient _apiClient;
    private List<CategoryModel>? _cachedCategories;

    public ApiCategorys(ITaixApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<List<CategoryModel>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedCategories != null)
            return _cachedCategories;

        _cachedCategories = await _apiClient.GetCategoriesAsync(cancellationToken);
        return _cachedCategories;
    }

    public void RefreshCache()
    {
        _cachedCategories = null;
    }

    public async Task<CategoryModel?> GetCategoryAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _apiClient.GetCategoryAsync(id, cancellationToken);
    }

    public async Task<CategoryModel> CreateAsync(CategoryModel category)
    {
        var result = await _apiClient.CreateCategoryAsync(category);
        RefreshCache();
        return result;
    }

    public async Task UpdateAsync(CategoryModel category)
    {
        await _apiClient.UpdateCategoryAsync(category);
        RefreshCache();
    }

    public async Task<CategoryModel> RestoreSystemCategoryAsync(int id)
    {
        var result = await _apiClient.RestoreSystemCategoryAsync(id);
        RefreshCache();
        return result;
    }

    public async Task DeleteAsync(CategoryModel category)
    {
        if (category.IsSystem) return;
        await _apiClient.DeleteCategoryAsync(category.ID);
        RefreshCache();
    }

    public async Task<int> ApplyDirectoryMatchAsync()
    {
        return await _apiClient.ApplyDirectoryMatchAsync();
    }
}

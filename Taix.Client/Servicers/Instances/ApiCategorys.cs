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

    public ApiCategorys(ITaixApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<List<CategoryModel>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        return await _apiClient.GetCategoriesAsync(cancellationToken);
    }

    public async Task<CategoryModel?> GetCategoryAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _apiClient.GetCategoryAsync(id, cancellationToken);
    }

    public async Task<CategoryModel> CreateAsync(CategoryModel category)
    {
        return await _apiClient.CreateCategoryAsync(category);
    }

    public async Task UpdateAsync(CategoryModel category)
    {
        await _apiClient.UpdateCategoryAsync(category);
    }

    public async Task<CategoryModel> RestoreSystemCategoryAsync(int id)
    {
        return await _apiClient.RestoreSystemCategoryAsync(id);
    }

    public async Task DeleteAsync(CategoryModel category)
    {
        if (category.IsSystem) return;
        await _apiClient.DeleteCategoryAsync(category.ID);
    }
}

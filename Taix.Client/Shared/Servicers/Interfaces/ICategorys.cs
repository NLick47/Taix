using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Taix.Client.Shared.Models;

namespace Taix.Client.Shared.Servicers.Interfaces;

public interface ICategorys
{
    Task<List<CategoryModel>> GetCategoriesAsync(CancellationToken cancellationToken = default);

    Task<CategoryModel?> GetCategoryAsync(int id, CancellationToken cancellationToken = default);

    Task<CategoryModel> CreateAsync(CategoryModel category);

    Task UpdateAsync(CategoryModel category);

    Task<CategoryModel> RestoreSystemCategoryAsync(int id);

    Task DeleteAsync(CategoryModel category);

    /// <summary>
    /// 清除分类缓存，下次获取时将重新从API加载
    /// </summary>
    void RefreshCache();
}

using System.Collections.Generic;
using System.Threading.Tasks;
using Taix.Client.Shared.Models;

namespace Taix.Client.Shared.Servicers.Interfaces;

public interface ICategorys
{
    Task<List<CategoryModel>> GetCategoriesAsync(bool containSystemCategory = false);

    Task<CategoryModel?> GetCategoryAsync(int id);

    Task<CategoryModel> CreateAsync(CategoryModel category);

    Task UpdateAsync(CategoryModel category);

    Task<CategoryModel> RestoreSystemCategoryAsync(int id);

    Task DeleteAsync(CategoryModel category);
}

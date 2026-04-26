using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Taix.Client.Librarys.Api;
using Taix.Client.Shared.Models;
using Taix.Client.Shared.Servicers.Interfaces;

namespace Taix.Client.Servicers.Instances;

public class ApiCategorys : ICategorys
{
    private readonly ITaixApiClient _apiClient;
    private readonly List<CategoryModel> _categories = new();
    private readonly object _lock = new();

    public ApiCategorys(ITaixApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public List<CategoryModel> GetCategories(bool containSystemCategory = false)
    {
        lock (_lock)
        {
            if (containSystemCategory)
            {
                var list = new List<CategoryModel>(_categories);
                var sys = list.FirstOrDefault(x => x.IsSystem);
                if (sys != null) sys.Name = ResourceStrings.Uncategorized;
                return list;
            }
            return new List<CategoryModel>(_categories.Where(x => !x.IsSystem));
        }
    }

    public async Task LoadAsync()
    {
        var categories = await _apiClient.GetCategoriesAsync(true);
        lock (_lock)
        {
            _categories.Clear();
            _categories.AddRange(categories);
        }
    }

    public CategoryModel GetCategory(int id)
    {
        lock (_lock)
        {
            var category = _categories.FirstOrDefault(m => m.ID == id);
            if (category != null && category.IsSystem) category.Name = ResourceStrings.Uncategorized;
            return category;
        }
    }

    public async Task<CategoryModel> CreateAsync(CategoryModel category)
    {
        var created = await _apiClient.CreateCategoryAsync(category);
        lock (_lock)
        {
            _categories.Add(created);
        }
        return created;
    }

    public async Task UpdateAsync(CategoryModel category)
    {
        await _apiClient.UpdateCategoryAsync(category);
        lock (_lock)
        {
            var index = _categories.FindIndex(c => c.ID == category.ID);
            if (index >= 0) _categories[index] = category;
        }
    }

    public async Task DeleteAsync(CategoryModel category)
    {
        if (category.IsSystem) return;
        await _apiClient.DeleteCategoryAsync(category.ID);
        lock (_lock)
        {
            _categories.RemoveAll(c => c.ID == category.ID);
        }
    }
}

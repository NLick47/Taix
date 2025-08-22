using Core.Models;

namespace Core.Servicers.Interfaces;

public interface ICategorys
{
    /// <summary>
    ///     获取所有分类
    /// </summary>
    /// <returns></returns>
    List<CategoryModel> GetCategories(bool containSystemCategory = false);

    /// <summary>
    ///     加载已存储的分类数据，仅建议在启动时调用一次，无必要请勿再次调用
    /// </summary>
    Task LoadAsync();

    CategoryModel GetCategory(int id);
    Task<CategoryModel> CreateAsync(CategoryModel category);
    Task UpdateAsync(CategoryModel category);
    Task DeleteAsync(CategoryModel category);
}
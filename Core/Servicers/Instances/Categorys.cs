using System.Diagnostics;
using Core.Librarys.SQLite;
using Core.Models;
using Core.Servicers.Interfaces;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using SharedLibrary;

namespace Core.Servicers.Instances;

public class Categorys : ICategorys
{
    private const string AppStateFilePath = "appstate.json";
    private readonly List<CategoryModel> _categories = new();
    private readonly object _lock = new();

    public async Task<CategoryModel> CreateAsync(CategoryModel category)
    {
        using var db = new TaiDbContext();
        await db.Categorys.AddAsync(category);
        await db.SaveChangesAsync();

        lock (_lock)
        {
            _categories.Add(category);
        }

        return category;
    }

    public async Task DeleteAsync(CategoryModel category)
    {
        if (category.ID == 0) return;
        using var db = new TaiDbContext();
        var item = await db.Categorys.FindAsync(category.ID);
        if (item != null)
        {
            db.Categorys.Remove(item);
            await db.SaveChangesAsync();

            lock (_lock)
            {
                _categories.RemoveAll(c => c.ID == category.ID);
            }
        }
    }

    public List<CategoryModel> GetCategories(bool containSystemCategory = false)
    {
        lock (_lock)
        {
            if (containSystemCategory)
            {
                _categories.First(x => x.ID == 0).Name = ResourceStrings.Uncategorized;
                return new List<CategoryModel>(_categories);
            }

            return new List<CategoryModel>(_categories.Where(x => x.ID != 0));
        }
    }

    public CategoryModel GetCategory(int id)
    {
        lock (_lock)
        {
            var category = _categories.FirstOrDefault(m => m.ID == id);
            if (category != null && id == 0) category.Name = ResourceStrings.Uncategorized;

            return category;
        }
    }

    public async Task LoadAsync()
    {
        Debug.WriteLine("开始加载分类");
        using var db = new TaiDbContext();

        await db.Database.EnsureCreatedAsync();
        var systemCategory = await LoadOrCreateSystemCategoryAsync(db);

        var dbCategories = await db.Categorys.ToListAsync();

        lock (_lock)
        {
            _categories.Clear();
            if (!_categories.Any(c => c.ID == systemCategory.ID)) _categories.Add(systemCategory);
            _categories.AddRange(dbCategories);
        }

        Debug.WriteLine($"分类加载完成，共{_categories.Count}个分类");
    }


    public async Task UpdateAsync(CategoryModel category)
    {
        if (category.ID == 0)
        {
            string json;
            lock (_lock)
            {
                var systemCategory = _categories.First(x => x.ID == 0);
                systemCategory.IconFile = category.IconFile;
                systemCategory.Color = category.Color;
                json = JsonConvert.SerializeObject(systemCategory);
            }

            await File.WriteAllTextAsync(AppStateFilePath, json);
            return;
        }

        using var db = new TaiDbContext();

        var existing = await db.Categorys.FindAsync(category.ID);
        if (existing != null)
        {
            db.Entry(existing).CurrentValues.SetValues(category);
            await db.SaveChangesAsync();

            lock (_lock)
            {
                var index = _categories.FindIndex(c => c.ID == category.ID);
                if (index >= 0) _categories[index] = existing;
            }
        }
    }

    private async Task<CategoryModel> LoadOrCreateSystemCategoryAsync(TaiDbContext db)
    {
        var defaultSystemCategory = CategoryModel.DefaultSystemCategory();
        if (!File.Exists(AppStateFilePath))
        {
            await using (File.Create(AppStateFilePath))
            {
            }

            await File.WriteAllTextAsync(AppStateFilePath, JsonConvert.SerializeObject(defaultSystemCategory));
        }

        CategoryModel systemCategory = null;
        try
        {
            var json = await File.ReadAllTextAsync(AppStateFilePath);
            systemCategory = JsonConvert.DeserializeObject<CategoryModel>(json) ?? defaultSystemCategory;
        }
        catch (JsonException)
        {
            await File.WriteAllTextAsync(AppStateFilePath, JsonConvert.SerializeObject(defaultSystemCategory));
            systemCategory = defaultSystemCategory;
        }
        catch (Exception ex)
        {
            return defaultSystemCategory;
        }

        return systemCategory;
    }
}
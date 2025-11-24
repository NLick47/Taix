using System.Collections.Concurrent;
using System.Diagnostics;
using Core.Librarys.SQLite;
using Core.Models;
using Core.Servicers.Interfaces;
using Microsoft.EntityFrameworkCore;
using SharedLibrary.Librarys;

namespace Core.Servicers.Instances;

public class AppData : IAppData
{
    private readonly ConcurrentDictionary<int, AppModel> _appsById;
    private readonly ConcurrentDictionary<string, AppModel> _appsByName;

    public AppData()
    {
        _appsById = new ConcurrentDictionary<int, AppModel>();
        _appsByName = new ConcurrentDictionary<string, AppModel>();
    }

    public List<AppModel> GetAllApps()
    {
        return _appsById.Values.ToList();
    }

    public async Task LoadAsync()
    {
        Debug.WriteLine("加载app开始");
        using (var db = new TaiDbContext())
        {
            var apps = await (from app in db.App
                join category in db.Categorys
                    on app.CategoryID equals category.ID into categoryGroup
                from n in categoryGroup.DefaultIfEmpty()
                select new AppModel
                {
                    ID = app.ID,
                    Category = n,
                    CategoryID = app.CategoryID,
                    Description = app.Description,
                    File = app.File,
                    IconFile = app.IconFile,
                    Name = app.Name,
                    Alias = app.Alias,
                    TotalTime = app.TotalTime
                }).ToListAsync();

            // 清空现有数据
            _appsById.Clear();
            _appsByName.Clear();

            // 批量添加
            foreach (var app in apps)
            {
                _appsById[app.ID] = app;
                _appsByName[app.Name] = app;
            }
        }
    }


    public void UpdateApp(AppModel app_)
    {
        try
        {
            using (var db = new TaiDbContext())
            {
                var app = db.App.FirstOrDefault(c => c.ID.Equals(app_.ID));
                if (app != null)
                {
                    app.TotalTime = app_.TotalTime;
                    app.IconFile = app_.IconFile;
                    app.Name = app_.Name;
                    app.Description = app_.Description;
                    app.File = app_.File;
                    app.CategoryID = app_.CategoryID;
                    app.Alias = app_.Alias;
                    db.SaveChanges();
                    
                    if (_appsById.TryGetValue(app_.ID, out var existingApp))
                    {
                        if (existingApp.Name != app_.Name)
                        {
                            _appsByName.TryRemove(existingApp.Name, out _);
                            _appsByName[app_.Name] = app_;
                        }
                        
                        // 更新对象属性
                        UpdateAppProperties(existingApp, app_);
                    }
                }
            }
        }
        catch (Exception e)
        {
            Logger.Error(e.ToString());
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

    public void AddApp(AppModel app)
    {
        if (_appsByName.TryAdd(app.Name, app))
        {
            try
            {
                using (var db = new TaiDbContext())
                {
                    var r = db.App.Add(app);
                    var res = db.SaveChanges();
                    if (res > 0)
                    {
                        _appsById[app.ID] = app;
                    }
                    else
                    {
                        _appsByName.TryRemove(app.Name, out _);
                    }
                }
            }
            catch (Exception e)
            {
                _appsByName.TryRemove(app.Name, out _);
                Logger.Error(e.ToString());
            }
        }
    }

    public List<AppModel> GetAppsByCategoryID(int categoryID)
    {
        return _appsById.Values
            .Where(m => m.CategoryID == categoryID)
            .ToList();
    }

    private void UpdateAppProperties(AppModel target, AppModel source)
    {
        target.TotalTime = source.TotalTime;
        target.IconFile = source.IconFile;
        target.Name = source.Name;
        target.Description = source.Description;
        target.File = source.File;
        target.CategoryID = source.CategoryID;
        target.Alias = source.Alias;
        target.Category = source.Category;
    }
}
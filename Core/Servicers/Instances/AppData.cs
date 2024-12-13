using Infrastructure.Librarys;
using Core.Librarys;
using Core.Librarys.SQLite;
using Core.Models;
using Core.Servicers.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Core.Servicers.Instances
{
    public class AppData : IAppData
    {
        private List<AppModel> _apps;

        private readonly object _locker = new object();

        public List<AppModel> GetAllApps()
        {
            return _apps;
        }

        public void Load()
        {
            Debug.WriteLine("加载app开始");
            using (var db = new TaiDbContext())
            {
                _apps = (from app in db.App
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
                         }).ToList();
            }
        }

        /// <summary>
        /// 更新app数据，要先调用GetApp获得后更改并传回才有效
        /// </summary>
        /// <param name="app"></param>
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
            lock (_locker)
            {
                return _apps.Where(m => m.Name == name).FirstOrDefault();
            }
        }
        public AppModel GetApp(int id)
        {
            lock (_locker)
            {
                return _apps.Where(m => m.ID == id).FirstOrDefault();
            }
        }
        public void AddApp(AppModel app)
        {
            lock (_locker)
            {
                if (_apps.Where(m => m.Name == app.Name).Any())
                {
                    return;
                }
                try
                {
                    using (var db = new TaiDbContext())
                    {
                        var r = db.App.Add(app);
                        int res = db.SaveChanges();
                        if (res > 0)
                        {

                            _apps.Add(app);
                        }
                    }
                }
                catch (Exception e)
                {
                    Logger.Error(e.ToString());
                }
            }
        }


        public List<AppModel> GetAppsByCategoryID(int categoryID)
        {
            lock (_locker)
            {
                return _apps.Where(m => m.CategoryID == categoryID).ToList();
            }
        }
    }
}

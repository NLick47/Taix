using Core.Models;
using Core.Models.Db;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Librarys.SQLite
{
    public class TaiDbContext : DbContext
    {
        /// <summary>
        /// 每日数据
        /// </summary>
        public DbSet<DailyLogModel> DailyLog { get; set; }
        /// <summary>
        /// 时段数据
        /// </summary>
        public DbSet<HoursLogModel> HoursLog { get; set; }
        public DbSet<AppModel> App { get; set; }
        /// <summary>
        /// 分类
        /// </summary>
        public DbSet<CategoryModel> Categorys { get; set; }
        /// <summary>
        /// 网站
        /// </summary>
        public DbSet<WebSiteModel> WebSites { get; set; }
        /// <summary>
        /// 网站分类
        /// </summary>
        public DbSet<WebSiteCategoryModel> WebSiteCategories { get; set; }
        /// <summary>
        /// 网页浏览记录（每小时）
        /// </summary>
        public DbSet<WebBrowseLogModel> WebBrowserLogs { get; set; }
        /// <summary>
        /// 网页链接
        /// </summary>
        public DbSet<WebUrlModel> WebUrls { get; set; }

        private static string _dbFilePath = Path.Combine(FileHelper.GetRootDirectory(), "Data", "data.db");


        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite($"Data Source={_dbFilePath}");
#if DEBUG
            optionsBuilder.LogTo(message => Debug.WriteLine(message));
#endif
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {

        }


    }
}
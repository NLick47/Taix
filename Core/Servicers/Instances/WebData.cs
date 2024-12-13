using Infrastructure.Librarys;
using Core.Librarys;
using Core.Librarys.Browser;
using Core.Librarys.SQLite;
using Core.Models.Data;
using Core.Models.Db;
using Core.Models.WebPage;
using Core.Servicers.Interfaces;
using CsvHelper;
using Newtonsoft.Json;
using Npoi.Mapper;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;

using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Core.Servicers.Instances
{
    public class WebData : IWebData
    {
        private readonly object _createUrlLocker = new object();

        #region AddUrlBrowseTime
        public async Task AddUrlBrowseTime(Site site_, int duration_, DateTime? dateTime_ = null)
        {
            Debug.WriteLine("AddUrlBrowseTime");
            try
            {
                if (string.IsNullOrEmpty(site_.Url))
                {
                    return;
                }

                var dateTime = dateTime_.HasValue ? dateTime_.Value : DateTime.Now;
                var logTime = new DateTime(dateTime.Year, dateTime.Month, dateTime.Day, dateTime.Hour, 0, 0);
                if (dateTime.Minute == 59 && dateTime.Second == 59)
                {
                    AddUrlBrowseTime(site_, duration_, logTime.AddHours(1));
                    return;
                }
                //  当前时段时长
                int nowTimeDuration = duration_;
                //  下一个时段时长
                int nextTimeDuration = 0;

                //  当前时段最大使用时长
                int nowTimeMaxDuration = (60 - dateTime.Minute) * 60;

                if (duration_ > 3600)
                {
                    nowTimeDuration = 3600;
                    nextTimeDuration = duration_ - 3600;
                }
                if (nowTimeDuration > nowTimeMaxDuration)
                {
                    nextTimeDuration += nowTimeDuration - nowTimeMaxDuration;

                    nowTimeDuration = nowTimeMaxDuration;
                }

                //var db = _database.GetContext("AddUrlBrowseTime");
                //using (var db = new TaiDbContext())
                using (var db = new TaiDbContext())
                {
                    //  获取站点信息
                    string domain = UrlHelper.GetDomain(site_.Url);
                    var site = db.WebSites.Where(m => m.Domain == domain).FirstOrDefault();
                    Debug.WriteLine("AddUrlBrowseTime 获取站点数据");

                    if (site == null)
                    {
                        db.WebSites.Add(new Models.Db.WebSiteModel()
                        {
                            Title = UrlHelper.GetName(site_.Url),
                            Domain = domain,
                        });

                        await db.SaveChangesAsync();
                    }

                    //  获取链接
                    var url = GetCreateUrl(db, site_);
                    if (url == null)
                    {
                        throw new Exception("在创建URL时异常");
                    }

                    //  记录
                    var log = await db.WebBrowserLogs.FirstOrDefaultAsync(m => m.LogTime == logTime && m.UrlId == url.ID);

                    if (log != null)
                    {
                        //  时长
                        int duration = log.Duration + nowTimeDuration;
                        //  判断是否超出
                        if (log.Duration + nowTimeDuration > 3600)
                        {
                            duration = 3600;
                            nextTimeDuration += log.Duration + nowTimeDuration - 3600;
                        }

                        //  更新记录
                        log.Duration = duration;
                    }
                    else
                    {
                        //  新记录
                        log = new Models.Db.WebBrowseLogModel()
                        {
                            Duration = nowTimeDuration,
                            UrlId = url.ID,
                            LogTime = logTime,
                            SiteId = site.ID,
                        };


                        db.WebBrowserLogs.Add(log);
                    }
                    site.Duration += nowTimeDuration;
                    await db.SaveChangesAsync();
                }
                if (nextTimeDuration > 0)
                {
                    AddUrlBrowseTime(site_, nextTimeDuration, logTime.AddHours(1));
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"在更新链接[{site_.Url}]时长[{duration_}]时异常，{ex}");
            }

        }
        #endregion

        #region UpdateUrlFavicon
        public async Task UpdateUrlFavicon(Site site_, string iconFile_)
        {
            if (string.IsNullOrEmpty(iconFile_) || string.IsNullOrEmpty(site_.Url))
            {
                return;
            }
            //  判断是否是主域名
            bool isIndex = UrlHelper.IsIndexUrl(site_.Url);

            ////  获取主域名
            //string domain = UrlHelper.GetDomain(site_.Url);

            //  获取站点信息
            using (var db = new TaiDbContext())
            {
                //  更新站点图标
                var site = await GetCreateWebSite(db, site_.Url);
                Debug.WriteLine("UpdateUrlFavicon 获取站点数据");
                if (site != null && (string.IsNullOrEmpty(site.IconFile) || (isIndex && site.IconFile != iconFile_)))
                {
                    site.IconFile = iconFile_;
                }

                //  更新链接图标
                var url = GetCreateUrl(db, site_);
                if (url != null)
                {
                    url.IconFile = iconFile_;
                }

                await db.SaveChangesAsync();
            }

        }
        #endregion

        #region GetCreateSite
        /// <summary>
        /// 输入URL获取站点信息,不存在时创建
        /// </summary>
        /// <param name="url_">URL</param>
        /// <returns></returns>
        private async Task<WebSiteModel> GetCreateWebSite(TaiDbContext db, string url_)
        {
            //  获取主域名
            string domain = UrlHelper.GetDomain(url_);
            var site = await db.WebSites.FirstOrDefaultAsync(m => m.Domain == domain);
            if (site == null)
            {
                db.WebSites.Add(new WebSiteModel()
                {
                    Title = UrlHelper.GetName(url_),
                    Domain = domain,
                });
                await db.SaveChangesAsync();
            }
            return site;
        }
        #endregion
        #region GetCreateUrl
        /// <summary>
        /// 获取url数据,不存在时创建
        /// </summary>
        /// <param name="url_"></param>
        /// <returns></returns>
        private WebUrlModel GetCreateUrl(TaiDbContext db, Site site_)
        {
            lock (_createUrlLocker)
            {
                var result = db.WebUrls.Where(m => m.Url == site_.Url).FirstOrDefault();
                if (result == null)
                {
                    db.WebUrls.Add(new()
                    {
                        Url = site_.Url,
                        Title = site_.Title,
                    });
                    db.SaveChanges();
                }
                else
                {
                    if (result.Title != site_.Title)
                    {
                        result.Title = site_.Title;
                        db.SaveChanges();
                    }
                }
                return result;

            }
        }
        #endregion

        #region GetDateRangeWebSiteList
        public async Task<IReadOnlyList<WebSiteModel>> GetDateRangeWebSiteList(DateTime start, DateTime end, int take = 0, int skip = -1, bool isTime_ = false)
        {
            if (isTime_)
            {
                start = new DateTime(start.Year, start.Month, start.Day, start.Hour, 0, 0);
                end = new DateTime(end.Year, end.Month, end.Day, end.Hour, 59, 59);
            }
            else
            {
                start = new DateTime(start.Year, start.Month, start.Day, 0, 0, 0);
                end = new DateTime(end.Year, end.Month, end.Day, 23, 59, 59);
            }
            using (var db = new TaiDbContext())
            {
                var data = db.WebBrowserLogs
                .GroupBy(m => m.SiteId)
                .Select(s => new
                {
                    Site = s.FirstOrDefault().Site,
                    Duration = s.Sum(m => m.Duration)
                });

                if (skip > 0)
                {
                    data = data.Skip(skip);
                }

                if (take > 0)
                {
                    data = data.Take(take);
                }

                data = data.OrderByDescending(m => m.Duration);

                //var list = await data.Select(s => new
                //{
                //    Alias = s.Site.Alias,
                //    Title = s.Site.Title,
                //    Domain = s.Site.Domain,
                //    CategoryID = s.Site.CategoryID,
                //    IconFile = s.Site.IconFile,
                //    ID = s.Site.ID,
                //    Duration = s.Duration,
                //}).ToListAsync();

                var list = await data.ToListAsync();
                var result = JsonConvert.DeserializeObject<IReadOnlyList<WebSiteModel>>(JsonConvert.SerializeObject(list));
                return result;

            }
        }


        #endregion

        #region GetWebSiteCategories
        public async Task<IReadOnlyList<WebSiteCategoryModel>> GetWebSiteCategories()
        {
            using (var db = new TaiDbContext())
            {
                var result = await db.WebSiteCategories.ToListAsync();
                return result.AsReadOnly();
            }
        }
        #endregion

        #region CreateWebSiteCategory
        public async Task<WebSiteCategoryModel> CreateWebSiteCategory(WebSiteCategoryModel data_)
        {
            using (var db = new TaiDbContext())
            {
                db.WebSiteCategories.Add(data_);
                await db.SaveChangesAsync();
                return data_;
            }
        }
        #endregion

        #region UpdateWebSiteCategory
        public async Task UpdateWebSiteCategory(WebSiteCategoryModel data_)
        {
            using (var db = new TaiDbContext())
            {
                db.Update(data_);
                await db.SaveChangesAsync();
            }
        }
        #endregion

        #region DeleteWebSiteCategory
        public async Task DeleteWebSiteCategory(WebSiteCategoryModel data_)
        {
            using (var db = new TaiDbContext())
            {
                var websitesToUpdate = db.WebSites.Where(site => site.CategoryID == data_.ID);
                foreach (var website in websitesToUpdate)
                {
                    website.CategoryID = 0;
                }
                await db.SaveChangesAsync();

                db.WebSiteCategories.Remove(data_);
                await db.SaveChangesAsync();
            }
        }
        #endregion
        #region GetWebSites
        public async Task<IReadOnlyList<WebSiteModel>> GetWebSites(int categoryId_)
        {
            using (var db = new TaiDbContext())
            {
                var result = await db.WebSites.Where(m => m.CategoryID == categoryId_).ToListAsync();
                return result.AsReadOnly();
            }
        }
        #endregion
        #region GetWebSitesCount
        public Task<int> GetWebSitesCount(int categoryId_)
        {
            using (var db = new TaiDbContext())
            {
                var result = db.WebSites.CountAsync(m => m.CategoryID == categoryId_);
                return result;
            }
        }

        #endregion
        #region GetUnSetCategoryWebSites
        public async Task<IReadOnlyList<WebSiteModel>> GetUnSetCategoryWebSites()
        {
            using (var db = new TaiDbContext())
            {
                var result = await db.WebSites.Where(m => m.CategoryID == 0).ToListAsync();
                return result.AsReadOnly();
            }
        }


        #endregion

        public async Task UpdateWebSitesCategory(int[] siteIds_, int categoryId_)
        {
            using var db = new TaiDbContext();
            var sitesToUpdate = await db.WebSites.Where(w => siteIds_.Contains(w.ID)).ToListAsync();
            foreach (var site in sitesToUpdate)
            {
                site.CategoryID = categoryId_;
            }
            await db.SaveChangesAsync();

        }

        public async Task<IReadOnlyList<InfrastructureDataModel>> GetCategoriesStatistics(DateTime start_, DateTime end_)
        {
            using (var db = new TaiDbContext())
            {
                var data = await db.WebBrowserLogs
                .Where(log => log.LogTime >= start_ && log.LogTime <= end_.AddDays(1).AddTicks(-1)) // 筛选 LogTime
                .Join(db.WebSites,
                      log => log.SiteId,
                      site => site.ID,
                      (log, site) => new { log, site }) // 内连接并创建匿名对象
                .Join(db.WebSiteCategories,
                      combined => combined.site.CategoryID,
                      category => category.ID,
                      (combined, category) => new
                      {
                          combined.log.Duration,
                          CategoryID = category.ID,
                          CategoryName = category.Name
                      })
                .GroupBy(x => new
                {
                    x.CategoryID,
                    x.CategoryName
                })
                .Select(group => new InfrastructureDataModel
                {
                    Value = group.Sum(x => x.Duration),
                    ID = group.Key.CategoryID,
                    Name = group.Key.CategoryName
                })
                .ToListAsync();
                //  未分类


                var noCategoryData = await db.WebBrowserLogs
                .Where(log => log.LogTime >= start_ && log.LogTime <= end_.AddDays(1).AddTicks(-1))
                .GroupJoin(db.WebSites,
                           log => log.SiteId,
                           site => site.ID,
                           (log, siteGroup) => new { log, siteGroup })
                .SelectMany(
                    combined => combined.siteGroup.DefaultIfEmpty(),
                    (combined, site) => new
                    {
                        combined.log.Duration,
                        Site = site
                    })
                .Where(x => x.Site != null && x.Site.CategoryID == 0)
                .GroupBy(x => new
                {
                    x.Site.CategoryID,
                    x.Site.Title
                })
                .Select(group => new InfrastructureDataModel
                {
                    Value = group.Sum(x => x.Duration),
                    ID = group.Key.CategoryID,
                    Name = group.Key.Title
                })
                .ToListAsync();
                var result = data.Concat(noCategoryData).ToList();
                return result.AsReadOnly();
            }
        }

        public Task<WebSiteCategoryModel> GetWebSiteCategory(int categoryId_)
        {
            using var db = new TaiDbContext();
            var result = db.WebSiteCategories.FirstOrDefaultAsync(m => m.ID == categoryId_);
            return result;
        }

        public async Task<IReadOnlyList<InfrastructureDataModel>> GetBrowseDataStatistics(DateTime start_, DateTime end_, int siteId_ = 0)
        {
            using var db = new TaiDbContext();
            var start = new DateTime(start_.Year, start_.Month, start_.Day, 0, 0, 0);
            var end = new DateTime(end_.Year, end_.Month, end_.Day, 23, 59, 59);
            var dbResult = db.WebBrowserLogs.Where(m => m.LogTime >= start && m.LogTime <= end);
            if (siteId_ > 0)
            {
                dbResult = dbResult.Where(m => m.SiteId == siteId_);
            }

            var data = await dbResult.GroupBy(m => m.LogTime)
            .Select(m => new
            {
                Value = m.Sum(s => s.Duration),
                Time = m.FirstOrDefault().LogTime
            }).ToListAsync();

            var result = new List<InfrastructureDataModel>();
            if (start_ == end_)
            {

                //  获取24小时数据
                for (int i = 0; i < 24; i++)
                {
                    var time = new DateTime(start_.Year, start_.Month, start_.Day, i, 0, 0);
                    var item = data.Where(m => m.Time == time).FirstOrDefault();
                    var dataItem = new InfrastructureDataModel()
                    {
                        ID = i,
                        Name = i.ToString(),
                        Value = item != null ? item.Value : 0,
                    };
                    result.Add(dataItem);
                }
            }
            else
            {
                //  获取日期范围数据
                var days = (end_ - start_).TotalDays + 1;
                if (days <= 31)
                {
                    //  按天
                    for (int i = 0; i < days; i++)
                    {
                        var date = start_.AddDays(i);
                        var startTime = new DateTime(date.Year, date.Month, date.Day, 0, 0, 0);
                        var endTime = new DateTime(date.Year, date.Month, date.Day, 23, 59, 59);

                        var value = data.Where(m => m.Time >= startTime && m.Time <= endTime).Sum(m => m.Value);
                        var dataItem = new InfrastructureDataModel()
                        {
                            ID = i,
                            Name = i.ToString(),
                            Value = value,
                        };
                        result.Add(dataItem);
                    }
                }
                else
                {
                    //  按月
                    for (int i = 0; i < 12; i++)
                    {
                        var date = new DateTime(start_.Year, i + 1, 1, 0, 0, 0);
                        var startTime = new DateTime(date.Year, date.Month, 1, 0, 0, 0);
                        var endTime = new DateTime(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month), 23, 59, 59);

                        var value = data.Where(m => m.Time >= startTime && m.Time <= endTime).Sum(m => m.Value);
                        var dataItem = new InfrastructureDataModel()
                        {
                            ID = i,
                            Name = i.ToString(),
                            Value = value,
                        };
                        result.Add(dataItem);
                    }
                }
            }
            return result.AsReadOnly();
        }

        private class CategoryStatisticModel
        {
            public int Duration { get; set; }
            public DateTime LogTime { get; set; }
            public int CategoryID { get; set; }
        }
        public async Task<IReadOnlyList<ColumnDataModel>> GetBrowseDataByCategoryStatistics(DateTime start_, DateTime end_)
        {
            using var db = new TaiDbContext();
            var start = new DateTime(start_.Year, start_.Month, start_.Day, 0, 0, 0);
            var end = new DateTime(end_.Year, end_.Month, end_.Day, 23, 59, 59);

            //  查询分类
            var categories = await (from log in db.WebBrowserLogs
                                    join site in db.WebSites on log.SiteId equals site.ID into siteGroup
                                    from site in siteGroup.DefaultIfEmpty()
                                    join category in db.WebSiteCategories on site.CategoryID equals category.ID into categoryGroup
                                    from category in categoryGroup.DefaultIfEmpty()
                                    where log.LogTime >= start && log.LogTime <= end.AddDays(1).AddTicks(-1)
                                    group log by new
                                    {
                                        CategoryID = site.CategoryID,
                                        LogTime = log.LogTime
                                    } into g
                                    select new
                                    {
                                        Duration = g.Sum(x => x.Duration),
                                        LogTime = g.Key.LogTime,
                                        CategoryID = g.Key.CategoryID
                                    }).ToListAsync();


            var data = await db.WebBrowserLogs
            .Where(log => log.LogTime >= start && log.LogTime <= end.AddDays(1).AddTicks(-1)) // 筛选 LogTime
            .GroupBy(log => new
            {
                log.LogTime,
                CategoryID = log.Site.CategoryID
            })
            .Select(group => new
            {
                Duration = group.Sum(log => log.Duration),
                LogTime = group.Key.LogTime,
                CategoryID = group.Key.CategoryID
            })
            .ToListAsync();
            var result = new List<ColumnDataModel>();

            if (start_ == end_)
            {
                //  获取24小时数据
                foreach (var category in categories)
                {
                    result.Add(new ColumnDataModel()
                    {
                        CategoryID = category.CategoryID,
                        Values = new double[24]
                    });
                }
                for (int i = 0; i < 24; i++)
                {
                    var time = new DateTime(start_.Year, start_.Month, start_.Day, i, 0, 0);
                    foreach (var category in categories)
                    {
                        var log = data.Where(m => m.CategoryID == category.CategoryID && m.LogTime == time).FirstOrDefault();
                        if (log != null)
                        {
                            var resultItem = result.Where(m => m.CategoryID == category.CategoryID).FirstOrDefault();
                            resultItem.Values[i] = log.Duration;
                        }
                    }
                }
            }
            else
            {
                //  获取日期范围数据
                int days = (int)(end_ - start_).TotalDays + 1;
                if (days <= 31)
                {
                    //  按天
                    foreach (var category in categories)
                    {
                        result.Add(new ColumnDataModel()
                        {
                            CategoryID = category.CategoryID,
                            Values = new double[days]
                        });
                    }

                    for (int i = 0; i < days; i++)
                    {
                        var date = start_.AddDays(i);
                        var startTime = new DateTime(date.Year, date.Month, date.Day, 0, 0, 0);
                        var endTime = new DateTime(date.Year, date.Month, date.Day, 23, 59, 59);

                        foreach (var category in categories)
                        {
                            var duration = data.Where(m => m.CategoryID == category.CategoryID && m.LogTime >= startTime && m.LogTime <= endTime).Sum(m => m.Duration);

                            var resultItem = result.Where(m => m.CategoryID == category.CategoryID).FirstOrDefault();
                            resultItem.Values[i] = duration;
                        }
                    }
                }
                else
                {
                    //  按月
                    foreach (var category in categories)
                    {
                        result.Add(new ColumnDataModel()
                        {
                            CategoryID = category.CategoryID,
                            Values = new double[12]
                        });
                    }
                    for (int i = 0; i < 12; i++)
                    {
                        var date = new DateTime(start_.Year, i + 1, 1, 0, 0, 0);
                        var startTime = new DateTime(date.Year, date.Month, 1, 0, 0, 0);
                        var endTime = new DateTime(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month), 23, 59, 59);

                        foreach (var category in categories)
                        {
                            var duration = data.Where(m => m.CategoryID == category.CategoryID && m.LogTime >= startTime && m.LogTime <= endTime).Sum(m => m.Duration);

                            var resultItem = result.Where(m => m.CategoryID == category.CategoryID).FirstOrDefault();
                            resultItem.Values[i] = duration;
                        }
                    }
                }
            }
            return result.AsReadOnly();

        }

        public async Task<int> GetBrowseDurationTotal(DateTime start_, DateTime end_)
        {
            start_ = new DateTime(start_.Year, start_.Month, start_.Day, 0, 0, 0);
            end_ = new DateTime(end_.Year, end_.Month, end_.Day, 23, 59, 59);

            using var db = new TaiDbContext();
            var data = db.WebBrowserLogs.Where(m => m.LogTime >= start_ && m.LogTime <= end_);
            var result = await data.AnyAsync() ? await data.SumAsync(m => m.Duration) : 0;
            return result;
        }

        public Task<int> GetBrowseSitesTotal(DateTime start_, DateTime end_)
        {
            start_ = new DateTime(start_.Year, start_.Month, start_.Day, 0, 0, 0);
            end_ = new DateTime(end_.Year, end_.Month, end_.Day, 23, 59, 59);
            using var db = new TaiDbContext();
            var result = db.WebBrowserLogs.Where(m => m.LogTime >= start_ && m.LogTime <= end_).GroupBy(m => m.SiteId).CountAsync();
            return result;
        }

        public Task<int> GetBrowsePagesTotal(DateTime start_, DateTime end_)
        {
            start_ = new DateTime(start_.Year, start_.Month, start_.Day, 0, 0, 0);
            end_ = new DateTime(end_.Year, end_.Month, end_.Day, 23, 59, 59);
            using var db = new TaiDbContext();
            var result = db.WebBrowserLogs.Where(m => m.LogTime >= start_ && m.LogTime <= end_).GroupBy(m => m.UrlId).CountAsync();
            return result;
        }

        public async Task<IReadOnlyList<WebBrowseLogModel>> GetBrowseLogList(DateTime start_, DateTime end_, int siteId_ = 0)
        {
            start_ = new DateTime(start_.Year, start_.Month, start_.Day, 0, 0, 0);
            end_ = new DateTime(end_.Year, end_.Month, end_.Day, 23, 59, 59);
            using var db = new TaiDbContext();

            var query = (await (from log in db.WebBrowserLogs
                                join url in db.WebUrls on log.UrlId equals url.ID
                                join site in db.WebSites on log.SiteId equals site.ID
                                where log.LogTime >= start_ && log.LogTime <= end_ && log.SiteId == siteId_
                                orderby log.LogTime descending
                                select new
                                {
                                    Duration = log.Duration,
                                    ID = log.ID,
                                    LogTime = log.LogTime,
                                    Site = site,
                                    SiteId = log.SiteId,
                                    Url = url,
                                    UrlId = log.UrlId,
                                }).ToListAsync())
                         .Select(s => new WebBrowseLogModel
                         {
                             Duration = s.Duration,
                             ID = s.ID,
                             LogTime = s.LogTime,
                             Site = s.Site,
                             SiteId = s.SiteId,
                             Url = s.Url,
                             UrlId = s.UrlId,
                         });

            var result = query.ToList();
            return result;
        }

        public Task<WebSiteModel> GetWebSite(int id_)
        {
            using var db = new TaiDbContext();
            return db.WebSites.FirstOrDefaultAsync(m => m.ID == id_);
        }

        public Task<WebSiteModel> GetWebSite(string domain)
        {
            using var db = new TaiDbContext();
            return db.WebSites.FirstOrDefaultAsync(m => m.Domain.ToLower() == domain.ToLower());

        }

        public async Task Clear(DateTime start_, DateTime end_)
        {
            end_ = new DateTime(end_.Year, end_.Month, DateTime.DaysInMonth(end_.Year, end_.Month));

            using var db = new TaiDbContext();

            var logsToDelete = db.WebBrowserLogs
            .Where(log => log.LogTime >= start_.Date && log.LogTime <= end_.Date.AddDays(1).AddTicks(-1));
            db.WebBrowserLogs.RemoveRange(logsToDelete);

            await db.SaveChangesAsync();
        }

        public async Task<IReadOnlyList<WebSiteModel>> GetWebSiteLogList(DateTime start_, DateTime end_)
        {
            start_ = new DateTime(start_.Year, start_.Month, start_.Day, 0, 0, 0);
            end_ = new DateTime(end_.Year, end_.Month, end_.Day, 23, 59, 59);
            using var db = new TaiDbContext();
            var query = await (from log in db.WebBrowserLogs
                               where log.LogTime >= start_ && log.LogTime <= end_
                               join site in db.WebSites on log.SiteId equals site.ID into newSite
                               join category in db.WebSiteCategories on log.Site.CategoryID equals category.ID into newCategory
                               from nc in newCategory.DefaultIfEmpty()
                               select new
                               {
                                   ID = log.ID,
                                   UrlId = log.UrlId,
                                   Duration = log.Duration,
                                   SiteId = log.SiteId,
                                   Site = log.Site,
                                   Category = nc
                               })
                         .ToListAsync();
            var result = query.GroupBy(m => m.SiteId).Select(s => new WebSiteModel
            {
                Duration = s.Sum(m => m.Duration),
                ID = s.FirstOrDefault().Site.ID,
                Title = s.FirstOrDefault().Site.Title,
                Domain = s.FirstOrDefault().Site.Domain,
                IconFile = s.FirstOrDefault().Site.IconFile,
                CategoryID = s.FirstOrDefault().Site.CategoryID,
                Category = s.FirstOrDefault().Category,
                Alias = s.FirstOrDefault().Site.Alias,
            }).ToList();

            return result;
        }

        public async Task Clear(int siteId_)
        {
            using var db = new TaiDbContext();

            var logsToDelete = db.WebBrowserLogs.Where(log => log.SiteId == siteId_);
            db.WebBrowserLogs.RemoveRange(logsToDelete);

            var websiteToUpdate = await db.WebSites.FirstOrDefaultAsync(ws => ws.ID == siteId_);
            if (websiteToUpdate != null)
            {
                websiteToUpdate.Duration = 0;
            }
            await db.SaveChangesAsync();
        }

        public async Task Export(string dir_, DateTime start_, DateTime end_)
        {
            start_ = new DateTime(start_.Year, start_.Month, 1, 0, 0, 0);
            end_ = new DateTime(end_.Year, end_.Month, DateTime.DaysInMonth(end_.Year, end_.Month), 23, 59, 59);

            using var db = new TaiDbContext();

            var webSiteData = (await db.WebBrowserLogs.Where(m => m.LogTime >= start_ && m.LogTime <= end_)
                .ToListAsync())
                .Select(m => new
                {
                    时间 = m.LogTime,
                    标题 = m.Url.Title,
                    网址 = m.Url.Url,
                    时长 = m.Duration,
                });

            var mapper = new Mapper();
            mapper.Put(webSiteData, "浏览记录");

            string name = $"Tai网页统计数据({start_.ToString("yyyy年MM月")}-{end_.ToString("yyyy年MM月")})";
            if (start_.Year == end_.Year && start_.Month == end_.Month)
            {
                name = $"Tai网页统计数据({start_.ToString("yyyy年MM月")})";
            }
            mapper.Save(Path.Combine(dir_, $"{name}.xlsx"), false);

            //  导出csv
            using (var writer = new StreamWriter(Path.Combine(dir_, $"{name}.csv"), false, System.Text.Encoding.UTF8))
            using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
            {
                await csv.WriteRecordsAsync(webSiteData);
            }
        }

        public async Task<WebSiteModel> Update(WebSiteModel website_)
        {
            using var db = new TaiDbContext();
            var website = await db.WebSites.FirstOrDefaultAsync(m => m.ID == website_.ID);
            if (website != null)
            {
                website.Alias = website_.Alias;
                website.Domain = website_.Domain;
                website.Title = website_.Title;
                await db.SaveChangesAsync();

            }
            return website;
        }
    }
}
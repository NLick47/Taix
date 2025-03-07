using Core.Models.Data;
using Core.Models.Db;
using Core.Models.WebPage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Core.Servicers.Interfaces
{
    public interface IWebData
    {
        /// <summary>
        /// 添加链接浏览时长
        /// </summary>
        /// <param name="site_">链接</param>
        /// <param name="time_">时长（秒）</param>
        Task AddUrlBrowseTimeAsync(Site site_, int duration_, DateTime? dateTime_ = null);
        /// <summary>
        /// 更新链接的图标
        /// </summary>
        /// <param name="site_"></param>
        /// <param name="iconFile_">本地图标相对路径</param>
        Task UpdateUrlFaviconAsync(Site site_, string iconFile_);

        /// <summary>
        /// 获取日期范围的站点浏览数据(浏览时长降序排序)
        /// </summary>
        /// <param name="start">开始日期</param>
        /// <param name="end">结束日期</param>
        /// <param name="take">读取条数</param>
        /// <param name="isTime">是否精确到时间</param>
        /// <returns></returns>
        Task<IReadOnlyList<WebSiteModel>> GetDateRangeWebSiteListAsync(DateTime start, DateTime end, int take = 0, int skip = -1, bool isTime_ = false);
        /// <summary>
        /// 获取网站所有分类
        /// </summary>
        /// <returns></returns>
        Task<IReadOnlyList<WebSiteCategoryModel>> GetWebSiteCategoriesAsync();
        /// <summary>
        /// 创建网站分类
        /// </summary>
        /// <param name="data_"></param>
        /// <returns></returns>
        Task<WebSiteCategoryModel> CreateWebSiteCategoryAsync(WebSiteCategoryModel data_);
        /// <summary>
        /// 更新网站分类
        /// </summary>
        /// <param name="data_"></param>
        /// <returns></returns>
        Task UpdateWebSiteCategoryAsync(WebSiteCategoryModel data_);
        /// <summary>
        /// 删除网站分类
        /// </summary>
        /// <param name="data_"></param>
        Task DeleteWebSiteCategoryAsync(WebSiteCategoryModel data_);
        /// <summary>
        /// 通过分类ID获取网站列表
        /// </summary>
        /// <param name="categoryId_"></param>
        /// <returns></returns>
        Task<IReadOnlyList<WebSiteModel>> GetWebSitesAsync(int categoryId_);
        /// <summary>
        /// 通过分类ID获取网站总数
        /// </summary>
        /// <param name="categoryId_"></param>
        /// <returns></returns>
        Task<int> GetWebSitesCountAsync(int categoryId_);
        /// <summary>
        /// 获取未设置分类的站点列表
        /// </summary>
        /// <returns></returns>
        Task<IReadOnlyList<WebSiteModel>> GetUnSetCategoryWebSitesAsync();
        /// <summary>
        /// 批量更新站点分类
        /// </summary>
        /// <param name="siteIds_"></param>
        /// <param name="categoryId_"></param>
        Task UpdateWebSitesCategoryAsync(int[] siteIds_, int categoryId_);
        /// <summary>
        /// 获取指定时间段分类统计数据
        /// </summary>
        /// <param name="start_"></param>
        /// <param name="end_"></param>
        /// <returns></returns>
        Task<IReadOnlyList<InfrastructureDataModel>> GetCategoriesStatisticsAsync(DateTime start_, DateTime end_);
        /// <summary>
        /// 通过分类ID获取分类数据
        /// </summary>
        /// <param name="categoryId_"></param>
        /// <returns></returns>
        Task<WebSiteCategoryModel> GetWebSiteCategoryAsync(int categoryId_);
        /// <summary>
        /// 获取指定时间段的浏览时长统计数据
        /// </summary>
        /// <param name="start_"></param>
        /// <param name="end_"></param>
        /// <returns></returns>
        Task<IReadOnlyList<InfrastructureDataModel>> GetBrowseDataStatisticsAsync(DateTime start_, DateTime end_, int siteId_ = 0);
        //List<WebSiteModel> GetWebSites(Expression<Func<WebSiteModel, bool>> predicate_);
        /// <summary>
        /// 获取指定时间段按照分类统计的浏览时长数据
        /// </summary>
        /// <param name="start_"></param>
        /// <param name="end_"></param>
        /// <returns></returns>
        Task<IReadOnlyList<ColumnDataModel>> GetBrowseDataByCategoryStatisticsAsync(DateTime start_, DateTime end_);
        /// <summary>
        /// 统计指定时间段的浏览时长
        /// </summary>
        /// <param name="start_"></param>
        /// <param name="end_"></param>
        /// <returns></returns>
        Task<int> GetBrowseDurationTotalAsync(DateTime start_, DateTime end_);
        /// <summary>
        /// 统计指定时间段的站点浏览数量
        /// </summary>
        /// <param name="start_"></param>
        /// <param name="end_"></param>
        /// <returns></returns>
        Task<int> GetBrowseSitesTotalAsync(DateTime start_, DateTime end_);
        /// <summary>
        /// 统计指定时间段的网页浏览数量
        /// </summary>
        /// <param name="start_"></param>
        /// <param name="end_"></param>
        /// <returns></returns>
        Task<int> GetBrowsePagesTotalAsync(DateTime start_, DateTime end_);
        /// <summary>
        /// 获取指定时间段的网页浏览记录
        /// </summary>
        /// <param name="start_"></param>
        /// <param name="end_"></param>
        /// <param name="siteId_"></param>
        /// <returns></returns>
        Task<IReadOnlyList<WebBrowseLogModel>> GetBrowseLogListAsync(DateTime start_, DateTime end_, int siteId_ = 0);
        /// <summary>
        /// 获取网站数据
        /// </summary>
        /// <param name="id_"></param>
        /// <returns></returns>
        Task<WebSiteModel> GetWebSiteAsync(int id_);
        /// <summary>
        /// 通过域名获取站点数据
        /// </summary>
        /// <param name="domain_"></param>
        /// <returns></returns>
        Task<WebSiteModel> GetWebSiteAsync(string domain_);
        /// <summary>
        /// 清空指定日期范围数据
        /// </summary>
        /// <param name="start_"></param>
        /// <param name="end_"></param>
        Task ClearAsync(DateTime start_, DateTime end_);

        Task<IReadOnlyList<WebSiteModel>> GetWebSiteLogListAsync(DateTime start_, DateTime end_);
        /// <summary>
        /// 清空所有统计数据
        /// </summary>
        /// <param name="siteId_">站点ID</param>
        Task ClearAsync(int siteId_);
        /// <summary>
        /// 导出数据
        /// </summary>
        /// <param name="dir_">导出目录</param>
        /// <param name="start_">开始时间</param>
        /// <param name="end_">结束时间</param>
        Task ExportAsync(string dir_, DateTime start_, DateTime end_,ExportOptions options);
        /// <summary>
        /// 更新站点数据
        /// </summary>
        /// <param name="website_"></param>
        Task<WebSiteModel> UpdateAsync(WebSiteModel website_);
    }
}

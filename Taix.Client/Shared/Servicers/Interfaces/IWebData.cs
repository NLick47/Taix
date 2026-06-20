using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Taix.Client.Shared.Models.Data;
using Taix.Client.Shared.Models.Web;
using Taix.Client.Shared.Models.WebPage;

namespace Taix.Client.Shared.Servicers.Interfaces;

public interface IWebData
{
    /// <summary>
    /// 网站分类列表发生增删改时触发，供依赖分类语料的下游（如全局搜索）失效自身缓存
    //</summary>
    event Action? CategoriesChanged;

    /// <summary>
    /// 添加链接浏览时长
    /// </summary>
    Task AddUrlBrowseTimeAsync(Site site, int duration, DateTime? dateTime = null);

    /// <summary>
    /// 更新链接的图标
    /// </summary>
    Task UpdateUrlFaviconAsync(Site site, string iconFile);

    /// <summary>
    /// 获取日期范围的站点浏览数据(浏览时长降序排序)
    /// </summary>
    Task<IReadOnlyList<WebSiteModel>> GetDateRangeWebSiteListAsync(DateTime start, DateTime end, int take = 0,
        int skip = -1, bool isTime = false, CancellationToken cancellationToken = default);



    /// <summary>
    /// 获取网站所有分类
    /// </summary>
    /// <returns></returns>
    Task<IReadOnlyList<WebSiteCategoryModel>> GetWebSiteCategoriesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 创建网站分类
    /// </summary>
    Task<WebSiteCategoryModel> CreateWebSiteCategoryAsync(WebSiteCategoryModel data);

    /// <summary>
    /// 更新网站分类
    /// </summary>
    Task UpdateWebSiteCategoryAsync(WebSiteCategoryModel data);

    /// <summary>
    /// 删除网站分类
    /// </summary>
    Task DeleteWebSiteCategoryAsync(WebSiteCategoryModel data);

    /// <summary>
    /// 通过分类ID获取网站列表
    /// </summary>
    Task<IReadOnlyCollection<WebSiteModel>> GetWebSitesAsync(int categoryId, CancellationToken cancellationToken = default);

    /// <summary>直接从远程 API 获取指定分类的网站数量</summary>
    Task<int> GetWebSitesCountAsync(int categoryId);

    /// <summary>
    /// 通过分类ID获取网站总数
    /// </summary>
    Task<int> GetWebSitesCount(int categoryId);

    /// <summary>
    /// 获取未设置分类的站点列表
    /// </summary>
    /// <returns></returns>
    Task<IReadOnlyCollection<WebSiteModel>> GetUnSetCategoryWebSitesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量更新站点分类
    /// </summary>
    Task UpdateWebSitesCategoryAsync(int[] siteIds, int categoryId);

    /// <summary>
    /// 获取指定时间段分类统计数据
    /// </summary>
    Task<IReadOnlyList<InfrastructureDataModel>> GetCategoriesStatisticsAsync(DateTime start, DateTime end, CancellationToken cancellationToken = default);

    /// <summary>
    /// 通过分类ID获取分类数据
    /// </summary>
    Task<WebSiteCategoryModel> GetWebSiteCategoryAsync(int categoryId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取指定时间段的浏览时长统计数据
    /// </summary>
    Task<IReadOnlyList<InfrastructureDataModel>> GetBrowseDataStatisticsAsync(DateTime start, DateTime end,
        int siteId = 0, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取指定时间段按照分类统计的浏览时长数据
    /// </summary>
    Task<IReadOnlyList<ColumnDataModel>> GetBrowseDataByCategoryStatisticsAsync(DateTime start, DateTime end, CancellationToken cancellationToken = default);

    /// <summary>
    /// 统计指定时间段的浏览时长
    /// </summary>
    Task<int> GetBrowseDurationTotalAsync(DateTime start, DateTime end, CancellationToken cancellationToken = default);

    /// <summary>
    /// 统计指定时间段的站点浏览数量
    /// </summary>
    Task<int> GetBrowseSitesTotalAsync(DateTime start, DateTime end, CancellationToken cancellationToken = default);

    /// <summary>
    /// 统计指定时间段的网页浏览数量
    /// </summary>
    Task<int> GetBrowsePagesTotalAsync(DateTime start, DateTime end, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取指定时间段的网页浏览记录
    /// </summary>
    Task<IReadOnlyList<WebBrowseLogModel>> GetBrowseLogListAsync(DateTime start, DateTime end, int siteId = 0, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取网站数据
    /// </summary>
    Task<WebSiteModel?> GetWebSiteAsync(int id);

    /// <summary>
    /// 通过域名获取站点数据
    /// </summary>
    Task<WebSiteModel?> GetWebSiteAsync(string domain);

    /// <summary>
    /// 清空指定日期范围数据
    /// </summary>
    Task ClearAsync(DateTime start, DateTime end);

    Task<IReadOnlyList<WebSiteModel>> GetWebSiteLogListAsync(DateTime start, DateTime end, CancellationToken cancellationToken = default);

    /// <summary>
    /// 清空所有统计数据
    /// </summary>
    Task ClearAsync(int siteId);

    /// <summary>
    /// 导出数据到Excel/CSV
    /// </summary>
    Task ExportAsync(string dir, DateTime start, DateTime end);

    /// <summary>
    /// 更新站点数据
    /// </summary>
    Task<WebSiteModel?> UpdateAsync(WebSiteModel website);

    /// <summary>
    /// 清除网站分类缓存，下次获取时将重新从API加载
    /// </summary>
    void RefreshCategoriesCache();

    /// <summary>
    /// 应用 URL 匹配规则到网站
    /// </summary>
    /// <returns>匹配并更新的网站数量</returns>
    Task<int> ApplyUrlMatchAsync(string[]? patterns = null);
}

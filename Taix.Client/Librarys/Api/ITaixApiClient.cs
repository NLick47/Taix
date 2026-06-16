using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Taix.Client.Shared.Models;
using Taix.Client.Shared.Models.Data;
using Taix.Client.Shared.Models.Web;

namespace Taix.Client.Librarys.Api;

public interface ITaixApiClient
{
    // AppTimer
    Task UpdateAppDurationAsync(string processName, int duration, DateTime startDateTime);

    // AppData
    Task<List<AppModel>> GetAllAppsAsync();
    Task<AppModel?> GetAppAsync(int id);
    Task<AppModel?> GetAppByNameAsync(string name);
    Task<AppModel> CreateAppAsync(AppModel app);
    Task UpdateAppAsync(AppModel app);
    Task<List<AppModel>> GetAppsByCategoryAsync(int categoryId);

    // Category
    Task<List<CategoryModel>> GetCategoriesAsync(CancellationToken cancellationToken = default);
    Task<CategoryModel?> GetCategoryAsync(int id, CancellationToken cancellationToken = default);
    Task<CategoryModel> CreateCategoryAsync(CategoryModel category);
    Task UpdateCategoryAsync(CategoryModel category);
    Task<CategoryModel> RestoreSystemCategoryAsync(int id);
    Task DeleteCategoryAsync(int id);
    Task<int> ApplyDirectoryMatchAsync();

    // Data
    Task<List<DailyLogModel>> GetDateRangeLogListAsync(DateTime start, DateTime end, int take = -1, int skip = -1, CancellationToken cancellationToken = default);
    Task<List<DailyLogModel>> GetProcessMonthLogListAsync(int appId, DateTime month, CancellationToken cancellationToken = default);
    Task ClearAppDataAsync(int appId, DateTime? month = null);
    Task ClearRangeAsync(DateTime start, DateTime end);
    Task<List<HoursLogModel>> GetTimeRangeLogListAsync(DateTime time, CancellationToken cancellationToken = default);
    Task<List<HoursLogModel>> GetHoursRangeLogListAsync(DateTime start, DateTime end, CancellationToken cancellationToken = default);
    Task<List<AppSessionModel>> GetAppSessionsAsync(DateTime start, DateTime end, CancellationToken cancellationToken = default);
    Task<double[]> GetRangeTotalDataAsync(DateTime start, DateTime end, CancellationToken cancellationToken = default);
    Task<double[]> GetMonthTotalDataAsync(DateTime year, CancellationToken cancellationToken = default);
    Task<int> GetDateRangeAppCountAsync(DateTime start, DateTime end, CancellationToken cancellationToken = default);
    Task<List<ColumnDataModel>> GetCategoryHoursDataAsync(DateTime date, CancellationToken cancellationToken = default);
    Task<List<ColumnDataModel>> GetCategoryRangeDataAsync(DateTime start, DateTime end, CancellationToken cancellationToken = default);
    Task<List<ColumnDataModel>> GetCategoryYearDataAsync(DateTime date, CancellationToken cancellationToken = default);
    Task<List<ColumnDataModel>> GetAppDayDataAsync(int appId, DateTime date, CancellationToken cancellationToken = default);
    Task<List<ColumnDataModel>> GetAppRangeDataAsync(int appId, DateTime start, DateTime end, CancellationToken cancellationToken = default);
    Task<List<ColumnDataModel>> GetAppYearDataAsync(int appId, DateTime date, CancellationToken cancellationToken = default);
    Task<ExportDataResult> GetExportDataAsync(DateTime start, DateTime end);

    // HealthCheck
    Task<bool> HealthCheckAsync();

    // WebData
    Task AddUrlBrowseTimeAsync(string url, string? title, int duration, DateTime? dateTime = null);
    Task<List<WebSiteModel>> GetWebSitesAsync(int? categoryId = null, CancellationToken cancellationToken = default);
    Task<List<WebSiteCategoryModel>> GetWebSiteCategoriesAsync(CancellationToken cancellationToken = default);
    Task<WebSiteCategoryModel> CreateWebSiteCategoryAsync(WebSiteCategoryModel data);
    Task UpdateWebSiteCategoryAsync(WebSiteCategoryModel data);
    Task DeleteWebSiteCategoryAsync(int id);
    Task<WebSiteModel?> GetWebSiteAsync(int id);
    Task<WebSiteModel?> GetWebSiteByDomainAsync(string domain);
    Task<WebSiteModel?> UpdateWebSiteAsync(WebSiteModel website);
    Task UpdateWebSitesCategoryAsync(int[] siteIds, int categoryId);
    Task<List<WebSiteModel>> GetUnSetCategoryWebSitesAsync(CancellationToken cancellationToken = default);
    Task ClearWebDataAsync(DateTime? start = null, DateTime? end = null, int? siteId = null);
    Task<List<WebSiteModel>> GetDateRangeWebSiteListAsync(DateTime start, DateTime end, int take = 0, int skip = -1, bool isTime = false, CancellationToken cancellationToken = default);
    Task<int> GetWebSitesCountAsync(int categoryId);
    Task<List<InfrastructureDataModel>> GetCategoriesStatisticsAsync(DateTime start, DateTime end, CancellationToken cancellationToken = default);
    Task<List<InfrastructureDataModel>> GetBrowseDataStatisticsAsync(DateTime start, DateTime end, int siteId = 0, CancellationToken cancellationToken = default);
    Task<List<ColumnDataModel>> GetBrowseDataByCategoryStatisticsAsync(DateTime start, DateTime end, CancellationToken cancellationToken = default);
    Task<int> GetBrowseDurationTotalAsync(DateTime start, DateTime end, CancellationToken cancellationToken = default);
    Task<int> GetBrowseSitesTotalAsync(DateTime start, DateTime end, CancellationToken cancellationToken = default);
    Task<int> GetBrowsePagesTotalAsync(DateTime start, DateTime end, CancellationToken cancellationToken = default);
    Task<List<WebBrowseLogModel>> GetBrowseLogListAsync(DateTime start, DateTime end, int siteId = 0, CancellationToken cancellationToken = default);
    Task<List<WebSiteModel>> GetWebSiteLogListAsync(DateTime start, DateTime end, CancellationToken cancellationToken = default);
    Task<WebExportDataResult> GetWebExportDataAsync(DateTime start, DateTime end);
    Task<int> ApplyUrlMatchAsync();
}

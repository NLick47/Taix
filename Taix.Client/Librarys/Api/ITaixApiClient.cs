using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Taix.Client.Shared.Models;
using Taix.Client.Shared.Models.Data;
using Taix.Client.Shared.Models.Db;

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
    Task<List<CategoryModel>> GetCategoriesAsync(bool containSystemCategory = false);
    Task<CategoryModel?> GetCategoryAsync(int id);
    Task<CategoryModel> CreateCategoryAsync(CategoryModel category);
    Task UpdateCategoryAsync(CategoryModel category);
    Task DeleteCategoryAsync(int id);

    // Data
    Task<List<DailyLogModel>> GetTodayLogListAsync();
    Task<List<DailyLogModel>> GetDateRangeLogListAsync(DateTime start, DateTime end, int take = -1, int skip = -1);
    Task<List<DailyLogModel>> GetThisWeekLogListAsync();
    Task<List<DailyLogModel>> GetLastWeekLogListAsync();
    Task<List<DailyLogModel>> GetProcessMonthLogListAsync(int appId, DateTime month);
    Task<DailyLogModel?> GetProcessDayAsync(int appId, DateTime day);
    Task ClearAppDataAsync(int appId, DateTime? month = null);
    Task ClearRangeAsync(DateTime start, DateTime end);
    Task<List<HoursLogModel>> GetTimeRangeLogListAsync(DateTime time);
    Task<double[]> GetRangeTotalDataAsync(DateTime start, DateTime end);
    Task<double[]> GetMonthTotalDataAsync(DateTime year);
    Task<int> GetDateRangeAppCountAsync(DateTime start, DateTime end);
    Task<List<ColumnDataModel>> GetCategoryHoursDataAsync(DateTime date);
    Task<List<ColumnDataModel>> GetCategoryRangeDataAsync(DateTime start, DateTime end);
    Task<List<ColumnDataModel>> GetCategoryYearDataAsync(DateTime date);
    Task<List<ColumnDataModel>> GetAppDayDataAsync(int appId, DateTime date);
    Task<List<ColumnDataModel>> GetAppRangeDataAsync(int appId, DateTime start, DateTime end);
    Task<List<ColumnDataModel>> GetAppYearDataAsync(int appId, DateTime date);
    Task<ExportDataResult> GetExportDataAsync(DateTime start, DateTime end);

    // HealthCheck
    Task<bool> HealthCheckAsync();

    // WebData
    Task AddUrlBrowseTimeAsync(string url, string? title, int duration, DateTime? dateTime = null);
    Task<List<WebSiteModel>> GetWebSitesAsync(int? categoryId = null);
    Task<List<WebSiteCategoryModel>> GetWebSiteCategoriesAsync(bool containSystemCategory = false);
    Task<WebSiteCategoryModel> CreateWebSiteCategoryAsync(WebSiteCategoryModel data);
    Task UpdateWebSiteCategoryAsync(WebSiteCategoryModel data);
    Task DeleteWebSiteCategoryAsync(int id);
    Task<WebSiteModel?> GetWebSiteAsync(int id);
    Task<WebSiteModel?> GetWebSiteByDomainAsync(string domain);
    Task<WebSiteModel?> UpdateWebSiteAsync(WebSiteModel website);
    Task UpdateWebSitesCategoryAsync(int[] siteIds, int categoryId);
    Task<List<WebSiteModel>> GetUnSetCategoryWebSitesAsync();
    Task ClearWebDataAsync(DateTime? start = null, DateTime? end = null, int? siteId = null);
    Task<List<WebSiteModel>> GetDateRangeWebSiteListAsync(DateTime start, DateTime end, int take = 0, int skip = -1, bool isTime = false);
    Task<int> GetWebSitesCountAsync(int categoryId);
    Task<List<InfrastructureDataModel>> GetCategoriesStatisticsAsync(DateTime start, DateTime end);
    Task<List<InfrastructureDataModel>> GetBrowseDataStatisticsAsync(DateTime start, DateTime end, int siteId = 0);
    Task<List<ColumnDataModel>> GetBrowseDataByCategoryStatisticsAsync(DateTime start, DateTime end);
    Task<int> GetBrowseDurationTotalAsync(DateTime start, DateTime end);
    Task<int> GetBrowseSitesTotalAsync(DateTime start, DateTime end);
    Task<int> GetBrowsePagesTotalAsync(DateTime start, DateTime end);
    Task<List<WebBrowseLogModel>> GetBrowseLogListAsync(DateTime start, DateTime end, int siteId = 0);
    Task<List<WebSiteModel>> GetWebSiteLogListAsync(DateTime start, DateTime end);
    Task<WebExportDataResult> GetWebExportDataAsync(DateTime start, DateTime end);
}

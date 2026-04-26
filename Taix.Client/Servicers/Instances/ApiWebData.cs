using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MiniExcelLibs;
using Taix.Client.Librarys.Api;
using Taix.Client.Shared.Models.Data;
using Taix.Client.Shared.Models.Db;
using Taix.Client.Shared.Models.WebPage;
using Taix.Client.Shared.Servicers.Interfaces;

namespace Taix.Client.Servicers.Instances;

public class ApiWebData : IWebData
{
    private readonly ITaixApiClient _apiClient;

    public ApiWebData(ITaixApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public Task AddUrlBrowseTimeAsync(Site site, int duration, DateTime? dateTime = null) =>
        _apiClient.AddUrlBrowseTimeAsync(site.Url, site.Title, duration, dateTime);

    public Task UpdateUrlFaviconAsync(Site site, string iconFile) => Task.CompletedTask;

    public async Task<IReadOnlyList<WebSiteModel>> GetDateRangeWebSiteListAsync(DateTime start, DateTime end, int take = 0, int skip = -1, bool isTime = false, CancellationToken cancellationToken = default)
    {
        var result = await _apiClient.GetDateRangeWebSiteListAsync(start, end, take, skip, isTime);
        return result.AsReadOnly();
    }

    public async Task<IReadOnlyList<WebSiteCategoryModel>> GetWebSiteCategoriesAsync(bool containSystemCategory = false)
    {
        var result = await _apiClient.GetWebSiteCategoriesAsync(containSystemCategory);
        return result.AsReadOnly();
    }

    public async Task<WebSiteCategoryModel> CreateWebSiteCategoryAsync(WebSiteCategoryModel data)
    {
        return await _apiClient.CreateWebSiteCategoryAsync(data);
    }

    public async Task UpdateWebSiteCategoryAsync(WebSiteCategoryModel data)
    {
        await _apiClient.UpdateWebSiteCategoryAsync(data);
    }

    public async Task DeleteWebSiteCategoryAsync(WebSiteCategoryModel data)
    {
        await _apiClient.DeleteWebSiteCategoryAsync(data.ID);
    }

    public async Task<IReadOnlyList<WebSiteModel>> GetWebSitesAsync(int categoryId)
    {
        var result = await _apiClient.GetWebSitesAsync(categoryId);
        return result.AsReadOnly();
    }

    public async Task<int> GetWebSitesCountAsync(int categoryId)
    {
        return await _apiClient.GetWebSitesCountAsync(categoryId);
    }

    public async Task<IReadOnlyList<WebSiteModel>> GetUnSetCategoryWebSitesAsync()
    {
        var result = await _apiClient.GetUnSetCategoryWebSitesAsync();
        return result.AsReadOnly();
    }

    public async Task UpdateWebSitesCategoryAsync(int[] siteIds, int categoryId)
    {
        await _apiClient.UpdateWebSitesCategoryAsync(siteIds, categoryId);
    }

    public async Task<IReadOnlyList<InfrastructureDataModel>> GetCategoriesStatisticsAsync(DateTime start, DateTime end, CancellationToken cancellationToken = default)
    {
        var result = await _apiClient.GetCategoriesStatisticsAsync(start, end);
        return result.AsReadOnly();
    }

    public async Task<WebSiteCategoryModel> GetWebSiteCategoryAsync(int categoryId, CancellationToken cancellationToken = default)
    {
        var result = await _apiClient.GetWebSiteCategoriesAsync(true);
        return result.FirstOrDefault(m => m.ID == categoryId)!;
    }

    public async Task<IReadOnlyList<InfrastructureDataModel>> GetBrowseDataStatisticsAsync(DateTime start, DateTime end, int siteId = 0)
    {
        var result = await _apiClient.GetBrowseDataStatisticsAsync(start, end, siteId);
        return result.AsReadOnly();
    }

    public async Task<IReadOnlyList<ColumnDataModel>> GetBrowseDataByCategoryStatisticsAsync(DateTime start, DateTime end, CancellationToken cancellationToken = default)
    {
        var result = await _apiClient.GetBrowseDataByCategoryStatisticsAsync(start, end);
        return result.AsReadOnly();
    }

    public async Task<int> GetBrowseDurationTotalAsync(DateTime start, DateTime end, CancellationToken cancellationToken = default)
    {
        return await _apiClient.GetBrowseDurationTotalAsync(start, end);
    }

    public async Task<int> GetBrowseSitesTotalAsync(DateTime start, DateTime end, CancellationToken cancellationToken = default)
    {
        return await _apiClient.GetBrowseSitesTotalAsync(start, end);
    }

    public async Task<int> GetBrowsePagesTotalAsync(DateTime start, DateTime end, CancellationToken cancellationToken = default)
    {
        return await _apiClient.GetBrowsePagesTotalAsync(start, end);
    }

    public async Task<IReadOnlyList<WebBrowseLogModel>> GetBrowseLogListAsync(DateTime start, DateTime end, int siteId = 0)
    {
        var result = await _apiClient.GetBrowseLogListAsync(start, end, siteId);
        return result.AsReadOnly();
    }

    public async Task<WebSiteModel> GetWebSiteAsync(int id)
    {
        var result = await _apiClient.GetWebSiteAsync(id);
        return result!;
    }

    public async Task<WebSiteModel> GetWebSiteAsync(string domain)
    {
        var result = await _apiClient.GetWebSiteByDomainAsync(domain);
        return result!;
    }

    public async Task ClearAsync(DateTime start, DateTime end)
    {
        await _apiClient.ClearWebDataAsync(start, end);
    }

    public async Task<IReadOnlyList<WebSiteModel>> GetWebSiteLogListAsync(DateTime start, DateTime end)
    {
        var result = await _apiClient.GetWebSiteLogListAsync(start, end);
        return result.AsReadOnly();
    }

    public async Task ClearAsync(int siteId)
    {
        await _apiClient.ClearWebDataAsync(siteId: siteId);
    }

    public async Task ExportAsync(string dir, DateTime start, DateTime end)
    {
        var data = await _apiClient.GetWebExportDataAsync(start, end);
        var prefix = "Taix";

        var rows = data.Logs.Select(log => new
        {
            Time = log.LogTime.ToString("yyyy-MM-dd HH:mm"),
            Title = log.Url?.Title ?? log.Site?.Title ?? string.Empty,
            Website = log.Site?.Domain ?? string.Empty,
            Duration = FormatDuration(log.Duration)
        });

        // Excel
        var excelPath = Path.Combine(dir, $"{prefix}_web_data.xlsx");
        MiniExcel.SaveAs(excelPath, rows);

        // CSV
        var csvPath = Path.Combine(dir, $"{prefix}_web_data.csv");
        MiniExcel.SaveAs(csvPath, rows, excelType: MiniExcelLibs.ExcelType.CSV);
    }

    private static string FormatDuration(int seconds)
    {
        var ts = TimeSpan.FromSeconds(seconds);
        return $"{ts.Hours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}";
    }

    public async Task<WebSiteModel> UpdateAsync(WebSiteModel website)
    {
        var result = await _apiClient.UpdateWebSiteAsync(website);
        return result!;
    }
}

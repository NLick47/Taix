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
    private readonly IWebSiteData _webSiteData;

    public ApiWebData(ITaixApiClient apiClient, IWebSiteData webSiteData)
    {
        _apiClient = apiClient;
        _webSiteData = webSiteData;
    }

    public Task AddUrlBrowseTimeAsync(Site site, int duration, DateTime? dateTime = null) =>
        _apiClient.AddUrlBrowseTimeAsync(site.Url, site.Title, duration, dateTime);

    public Task UpdateUrlFaviconAsync(Site site, string iconFile) => Task.CompletedTask;

    public async Task<IReadOnlyList<WebSiteModel>> GetDateRangeWebSiteListAsync(DateTime start, DateTime end, int take = 0, int skip = -1, bool isTime = false, CancellationToken cancellationToken = default)
    {
        var result = await _apiClient.GetDateRangeWebSiteListAsync(start, end, take, skip, isTime, cancellationToken);
        return result.AsReadOnly();
    }

    public async Task<IReadOnlyList<WebSiteCategoryModel>> GetWebSiteCategoriesAsync(bool containSystemCategory = false, CancellationToken cancellationToken = default)
    {
        var result = await _apiClient.GetWebSiteCategoriesAsync(containSystemCategory, cancellationToken);
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

    public async Task<IReadOnlyCollection<WebSiteModel>> GetWebSitesAsync(int categoryId, CancellationToken cancellationToken = default)
    {
        return await _webSiteData.GetWebSitesByCategoryIDAsync(categoryId);
    }

    public async Task<int> GetWebSitesCount(int categoryId)
    {
        var sites = await _webSiteData.GetWebSitesByCategoryIDAsync(categoryId);
        return sites.Count;
    }

    public async Task<int> GetWebSitesCountAsync(int categoryId)
    {
        return await _apiClient.GetWebSitesCountAsync(categoryId);
    }

    public async Task<IReadOnlyCollection<WebSiteModel>> GetUnSetCategoryWebSitesAsync(CancellationToken cancellationToken = default)
    {
        return await _webSiteData.GetWebSitesByCategoryIDAsync(0);
    }

    public async Task UpdateWebSitesCategoryAsync(int[] siteIds, int categoryId)
    {
        await _apiClient.UpdateWebSitesCategoryAsync(siteIds, categoryId);
    }

    public async Task<IReadOnlyList<InfrastructureDataModel>> GetCategoriesStatisticsAsync(DateTime start, DateTime end, CancellationToken cancellationToken = default)
    {
        var result = await _apiClient.GetCategoriesStatisticsAsync(start, end, cancellationToken);
        return result.AsReadOnly();
    }

    public async Task<WebSiteCategoryModel> GetWebSiteCategoryAsync(int categoryId, CancellationToken cancellationToken = default)
    {
        var result = await _apiClient.GetWebSiteCategoriesAsync(true);
        return result.FirstOrDefault(m => m.ID == categoryId)!;
    }

    public async Task<IReadOnlyList<InfrastructureDataModel>> GetBrowseDataStatisticsAsync(DateTime start, DateTime end, int siteId = 0, CancellationToken cancellationToken = default)
    {
        var result = await _apiClient.GetBrowseDataStatisticsAsync(start, end, siteId, cancellationToken);
        return result.AsReadOnly();
    }

    public async Task<IReadOnlyList<ColumnDataModel>> GetBrowseDataByCategoryStatisticsAsync(DateTime start, DateTime end, CancellationToken cancellationToken = default)
    {
        var result = await _apiClient.GetBrowseDataByCategoryStatisticsAsync(start, end, cancellationToken);
        return result.AsReadOnly();
    }

    public async Task<int> GetBrowseDurationTotalAsync(DateTime start, DateTime end, CancellationToken cancellationToken = default)
    {
        return await _apiClient.GetBrowseDurationTotalAsync(start, end, cancellationToken);
    }

    public async Task<int> GetBrowseSitesTotalAsync(DateTime start, DateTime end, CancellationToken cancellationToken = default)
    {
        return await _apiClient.GetBrowseSitesTotalAsync(start, end, cancellationToken);
    }

    public async Task<int> GetBrowsePagesTotalAsync(DateTime start, DateTime end, CancellationToken cancellationToken = default)
    {
        return await _apiClient.GetBrowsePagesTotalAsync(start, end, cancellationToken);
    }

    public async Task<IReadOnlyList<WebBrowseLogModel>> GetBrowseLogListAsync(DateTime start, DateTime end, int siteId = 0, CancellationToken cancellationToken = default)
    {
        var result = await _apiClient.GetBrowseLogListAsync(start, end, siteId, cancellationToken);
        return result.AsReadOnly();
    }

    public async Task<WebSiteModel?> GetWebSiteAsync(int id)
    {
        return await _webSiteData.GetWebSiteAsync(id);
    }

    public async Task<WebSiteModel?> GetWebSiteAsync(string domain)
    {
        return await _webSiteData.GetWebSiteByDomainAsync(domain);
    }

    public async Task ClearAsync(DateTime start, DateTime end)
    {
        await _apiClient.ClearWebDataAsync(start, end);
    }

    public async Task<IReadOnlyList<WebSiteModel>> GetWebSiteLogListAsync(DateTime start, DateTime end, CancellationToken cancellationToken = default)
    {
        var result = await _apiClient.GetWebSiteLogListAsync(start, end, cancellationToken);
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
        var uncategorized = ResourceStrings.Uncategorized;

        var rows = data.Logs.Select(log => new WebLogExportRow
        {
            Time = DateTime.SpecifyKind(log.LogTime, DateTimeKind.Utc).ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
            Title = log.Url?.Title ?? log.Site?.Title ?? string.Empty,
            Website = log.Site?.Domain ?? string.Empty,
            Duration = FormatDuration(log.Duration),
            Category = log.Site?.Category?.Name ?? uncategorized
        });

        var rangePart = $"{start.ToString("yyyyMMdd")}_{end.ToString("yyyyMMdd")}";
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

        // Excel
        var excelPath = Path.Combine(dir, $"{prefix}_web_data_{rangePart}_{timestamp}.xlsx");
        MiniExcel.SaveAs(excelPath, rows);

        // CSV
        var csvPath = Path.Combine(dir, $"{prefix}_web_data_{rangePart}_{timestamp}.csv");
        MiniExcel.SaveAs(csvPath, rows, excelType: MiniExcelLibs.ExcelType.CSV);
    }

    private static string FormatDuration(int seconds)
    {
        var ts = TimeSpan.FromSeconds(seconds);
        return $"{ts.Hours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}";
    }

    public async Task<WebSiteModel?> UpdateAsync(WebSiteModel website)
    {
        return await _webSiteData.UpdateWebSiteAsync(website);
    }
}

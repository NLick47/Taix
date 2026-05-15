using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Taix.Client.Shared.Librarys;
using Taix.Client.Shared.Models;
using Taix.Client.Shared.Models.Data;
using Taix.Client.Shared.Models.Db;

namespace Taix.Client.Librarys.Api;

public class TaixApiClient : ITaixApiClient
{
    private readonly HttpClient _httpClient;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        TypeInfoResolver = TaixApiJsonContext.Default
    };

    public TaixApiClient(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("TaixApi");
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026")]
    [UnconditionalSuppressMessage("AOT", "IL3050")]
    private async Task<TResponse> GetAsync<TResponse>(string url, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var result = JsonSerializer.Deserialize<ApiResponse<TResponse>>(json, JsonOptions);
        if (result == null) throw new InvalidOperationException("Empty response");
        if (result.Code != 0) throw new InvalidOperationException(result.Message);
        return result.Data!;
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026")]
    [UnconditionalSuppressMessage("AOT", "IL3050")]
    private async Task PostAsync<TRequest>(string url, TRequest request)
    {
        var json = JsonSerializer.Serialize(request, JsonOptions);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync(url, content);
        response.EnsureSuccessStatusCode();
        var responseJson = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ApiResponse>(responseJson, JsonOptions);
        if (result == null) throw new InvalidOperationException("Empty response");
        if (result.Code != 0) throw new InvalidOperationException(result.Message);
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026")]
    [UnconditionalSuppressMessage("AOT", "IL3050")]
    private async Task<TResponse> PostAsync<TResponse, TRequest>(string url, TRequest request)
    {
        var json = JsonSerializer.Serialize(request, JsonOptions);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync(url, content);
        response.EnsureSuccessStatusCode();
        var responseJson = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ApiResponse<TResponse>>(responseJson, JsonOptions);
        if (result == null) throw new InvalidOperationException("Empty response");
        if (result.Code != 0) throw new InvalidOperationException(result.Message);
        return result.Data!;
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026")]
    [UnconditionalSuppressMessage("AOT", "IL3050")]
    private async Task<TResponse> PostEmptyAsync<TResponse>(string url)
    {
        using var response = await _httpClient.PostAsync(url, null);
        response.EnsureSuccessStatusCode();
        var responseJson = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ApiResponse<TResponse>>(responseJson, JsonOptions);
        if (result == null) throw new InvalidOperationException("Empty response");
        if (result.Code != 0) throw new InvalidOperationException(result.Message);
        return result.Data!;
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026")]
    [UnconditionalSuppressMessage("AOT", "IL3050")]
    private async Task PutAsync<TRequest>(string url, TRequest request)
    {
        var json = JsonSerializer.Serialize(request, JsonOptions);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PutAsync(url, content);
        response.EnsureSuccessStatusCode();
        var responseJson = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ApiResponse>(responseJson, JsonOptions);
        if (result == null) throw new InvalidOperationException("Empty response");
        if (result.Code != 0) throw new InvalidOperationException(result.Message);
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026")]
    [UnconditionalSuppressMessage("AOT", "IL3050")]
    private async Task<TResponse> PutAsync<TResponse, TRequest>(string url, TRequest request)
    {
        var json = JsonSerializer.Serialize(request, JsonOptions);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PutAsync(url, content);
        response.EnsureSuccessStatusCode();
        var responseJson = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ApiResponse<TResponse>>(responseJson, JsonOptions);
        if (result == null) throw new InvalidOperationException("Empty response");
        if (result.Code != 0) throw new InvalidOperationException(result.Message);
        return result.Data!;
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026")]
    [UnconditionalSuppressMessage("AOT", "IL3050")]
    private async Task DeleteAsync(string url)
    {
        using var response = await _httpClient.DeleteAsync(url);
        response.EnsureSuccessStatusCode();
        var responseJson = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ApiResponse>(responseJson, JsonOptions);
        if (result == null) throw new InvalidOperationException("Empty response");
        if (result.Code != 0) throw new InvalidOperationException(result.Message);
    }


    public async Task<bool> HealthCheckAsync()
    {
        try
        {
            using var response = await _httpClient.GetAsync("api/health");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    // AppTimer
    public async Task UpdateAppDurationAsync(string processName, int duration, DateTime startDateTime)
    {
        await PostAsync("api/apptimer/duration", new UpdateAppDurationRequest
        {
            ProcessName = processName,
            Duration = duration,
            StartDateTime = startDateTime.ToUniversalTime()
        });
    }

    // AppData
    public Task<List<AppModel>> GetAllAppsAsync() => GetAsync<List<AppModel>>("api/appdata");
    public Task<AppModel?> GetAppAsync(int id) => GetAsync<AppModel?>($"api/appdata/{id}");
    public Task<AppModel?> GetAppByNameAsync(string name) => GetAsync<AppModel?>($"api/appdata/by-name/{Uri.EscapeDataString(name)}");

    public async Task<AppModel> CreateAppAsync(AppModel app)
    {
        return await PostAsync<AppModel, CreateAppRequest>("api/appdata", new CreateAppRequest
        {
            Name = app.Name,
            Description = app.Description,
            File = app.File,
            IconFile = app.IconFile,
            CategoryID = app.CategoryID
        });
    }

    public async Task UpdateAppAsync(AppModel app)
    {
        await PutAsync<UpdateAppRequest>($"api/appdata/{app.ID}", new UpdateAppRequest
        {
            ID = app.ID,
            Name = app.Name,
            Alias = app.Alias,
            Description = app.Description,
            File = app.File,
            IconFile = app.IconFile,
            CategoryID = app.CategoryID,
            TotalTime = app.TotalTime
        });
    }

    public Task<List<AppModel>> GetAppsByCategoryAsync(int categoryId) =>
        GetAsync<List<AppModel>>($"api/appdata/by-category/{categoryId}");

    // Category
    public Task<List<CategoryModel>> GetCategoriesAsync(CancellationToken cancellationToken = default) =>
        GetAsync<List<CategoryModel>>("api/category", cancellationToken);

    public Task<CategoryModel?> GetCategoryAsync(int id, CancellationToken cancellationToken = default) =>
        GetAsync<CategoryModel?>($"api/category/{id}", cancellationToken);

    public async Task<CategoryModel> CreateCategoryAsync(CategoryModel category)
    {
        return await PostAsync<CategoryModel, CreateCategoryRequest>("api/category", new CreateCategoryRequest
        {
            Name = category.Name,
            IconFile = category.IconFile,
            Color = category.Color,
            IsDirectoryMatch = category.IsDirectoryMatch,
            Directories = category.Directories
        });
    }

    public async Task UpdateCategoryAsync(CategoryModel category)
    {
        await PutAsync<UpdateCategoryRequest>($"api/category/{category.ID}", new UpdateCategoryRequest
        {
            ID = category.ID,
            Name = category.Name,
            IconFile = category.IconFile,
            Color = category.Color,
            IsDirectoryMatch = category.IsDirectoryMatch,
            Directories = category.Directories
        });
    }

    public Task<CategoryModel> RestoreSystemCategoryAsync(int id) =>
        PostEmptyAsync<CategoryModel>($"api/category/{id}/restore");

    public Task DeleteCategoryAsync(int id) => DeleteAsync($"api/category/{id}");

    // Data
    private static string TzQuery(string url)
    {
        var sep = url.Contains('?') ? "&" : "?";
        return $"{url}{sep}timezone={Uri.EscapeDataString(TimeZoneHelper.GetIanaTimeZoneId())}";
    }

    public Task<List<DailyLogModel>> GetDateRangeLogListAsync(DateTime start, DateTime end, int take = -1, int skip = -1, CancellationToken cancellationToken = default) =>
        GetAsync<List<DailyLogModel>>(TzQuery($"api/data/range?start={Uri.EscapeDataString(start.ToString("yyyy-MM-ddTHH:mm:ss"))}&end={Uri.EscapeDataString(end.ToString("yyyy-MM-ddTHH:mm:ss"))}&take={take}&skip={skip}"), cancellationToken);

    public Task<List<DailyLogModel>> GetProcessMonthLogListAsync(int appId, DateTime month, CancellationToken cancellationToken = default) =>
        GetAsync<List<DailyLogModel>>(TzQuery($"api/data/process-month?appId={appId}&month={Uri.EscapeDataString(month.ToString("yyyy-MM-ddTHH:mm:ss"))}"), cancellationToken);

    public Task ClearAppDataAsync(int appId, DateTime? month = null) =>
        DeleteAsync(month.HasValue
            ? TzQuery($"api/data/clear/{appId}?month={Uri.EscapeDataString(month.Value.ToString("yyyy-MM-ddTHH:mm:ss"))}")
            : TzQuery($"api/data/clear/{appId}"));

    public Task ClearRangeAsync(DateTime start, DateTime end) =>
        DeleteAsync(TzQuery($"api/data/clear-range?start={Uri.EscapeDataString(start.ToString("yyyy-MM-ddTHH:mm:ss"))}&end={Uri.EscapeDataString(end.ToString("yyyy-MM-ddTHH:mm:ss"))}"));

    public Task<List<HoursLogModel>> GetTimeRangeLogListAsync(DateTime time, CancellationToken cancellationToken = default) =>
        GetAsync<List<HoursLogModel>>(TzQuery($"api/data/time-range?time={Uri.EscapeDataString(time.ToString("yyyy-MM-ddTHH:mm:ss"))}"), cancellationToken);

    public Task<List<HoursLogModel>> GetHoursRangeLogListAsync(DateTime start, DateTime end, CancellationToken cancellationToken = default) =>
        GetAsync<List<HoursLogModel>>(TzQuery($"api/data/hours-range?start={Uri.EscapeDataString(start.ToString("yyyy-MM-ddTHH:mm:ss"))}&end={Uri.EscapeDataString(end.ToString("yyyy-MM-ddTHH:mm:ss"))}"), cancellationToken);

    public Task<List<AppSessionModel>> GetAppSessionsAsync(DateTime start, DateTime end, CancellationToken cancellationToken = default) =>
        GetAsync<List<AppSessionModel>>(TzQuery($"api/data/sessions?start={Uri.EscapeDataString(start.ToString("yyyy-MM-ddTHH:mm:ss"))}&end={Uri.EscapeDataString(end.ToString("yyyy-MM-ddTHH:mm:ss"))}"), cancellationToken);

    public Task<double[]> GetRangeTotalDataAsync(DateTime start, DateTime end, CancellationToken cancellationToken = default) =>
        GetAsync<double[]>(TzQuery($"api/data/range-total?start={Uri.EscapeDataString(start.ToString("yyyy-MM-ddTHH:mm:ss"))}&end={Uri.EscapeDataString(end.ToString("yyyy-MM-ddTHH:mm:ss"))}"), cancellationToken);

    public Task<double[]> GetMonthTotalDataAsync(DateTime year, CancellationToken cancellationToken = default) =>
        GetAsync<double[]>(TzQuery($"api/data/month-total?year={Uri.EscapeDataString(year.ToString("yyyy-MM-ddTHH:mm:ss"))}"), cancellationToken);

    public Task<int> GetDateRangeAppCountAsync(DateTime start, DateTime end, CancellationToken cancellationToken = default) =>
        GetAsync<int>(TzQuery($"api/data/range-app-count?start={Uri.EscapeDataString(start.ToString("yyyy-MM-ddTHH:mm:ss"))}&end={Uri.EscapeDataString(end.ToString("yyyy-MM-ddTHH:mm:ss"))}"), cancellationToken);

    public Task<List<ColumnDataModel>> GetCategoryHoursDataAsync(DateTime date, CancellationToken cancellationToken = default) =>
        GetAsync<List<ColumnDataModel>>(TzQuery($"api/data/category-hours?date={Uri.EscapeDataString(date.ToString("yyyy-MM-ddTHH:mm:ss"))}"), cancellationToken);

    public Task<List<ColumnDataModel>> GetCategoryRangeDataAsync(DateTime start, DateTime end, CancellationToken cancellationToken = default) =>
        GetAsync<List<ColumnDataModel>>(TzQuery($"api/data/category-range?start={Uri.EscapeDataString(start.ToString("yyyy-MM-ddTHH:mm:ss"))}&end={Uri.EscapeDataString(end.ToString("yyyy-MM-ddTHH:mm:ss"))}"), cancellationToken);

    public Task<List<ColumnDataModel>> GetCategoryYearDataAsync(DateTime date, CancellationToken cancellationToken = default) =>
        GetAsync<List<ColumnDataModel>>(TzQuery($"api/data/category-year?date={Uri.EscapeDataString(date.ToString("yyyy-MM-ddTHH:mm:ss"))}"), cancellationToken);

    public Task<List<ColumnDataModel>> GetAppDayDataAsync(int appId, DateTime date, CancellationToken cancellationToken = default) =>
        GetAsync<List<ColumnDataModel>>(TzQuery($"api/data/app-day?appId={appId}&date={Uri.EscapeDataString(date.ToString("yyyy-MM-ddTHH:mm:ss"))}"), cancellationToken);

    public Task<List<ColumnDataModel>> GetAppRangeDataAsync(int appId, DateTime start, DateTime end, CancellationToken cancellationToken = default) =>
        GetAsync<List<ColumnDataModel>>(TzQuery($"api/data/app-range?appId={appId}&start={Uri.EscapeDataString(start.ToString("yyyy-MM-ddTHH:mm:ss"))}&end={Uri.EscapeDataString(end.ToString("yyyy-MM-ddTHH:mm:ss"))}"), cancellationToken);

    public Task<List<ColumnDataModel>> GetAppYearDataAsync(int appId, DateTime date, CancellationToken cancellationToken = default) =>
        GetAsync<List<ColumnDataModel>>(TzQuery($"api/data/app-year?appId={appId}&date={Uri.EscapeDataString(date.ToString("yyyy-MM-ddTHH:mm:ss"))}"), cancellationToken);

    public Task<ExportDataResult> GetExportDataAsync(DateTime start, DateTime end) =>
        GetAsync<ExportDataResult>(TzQuery($"api/data/export?start={Uri.EscapeDataString(start.ToString("yyyy-MM-ddTHH:mm:ss"))}&end={Uri.EscapeDataString(end.ToString("yyyy-MM-ddTHH:mm:ss"))}"));

    // WebData
    public async Task AddUrlBrowseTimeAsync(string url, string? title, int duration, DateTime? dateTime = null)
    {
        await PostAsync("api/webdata/browse-time", new AddUrlBrowseTimeRequest
        {
            Url = url,
            Title = title,
            Duration = duration,
            DateTime = dateTime?.ToUniversalTime()
        });
    }

    public Task<List<WebSiteModel>> GetWebSitesAsync(int? categoryId = null, CancellationToken cancellationToken = default) =>
        GetAsync<List<WebSiteModel>>(categoryId.HasValue ? $"api/webdata/sites?categoryId={categoryId}" : "api/webdata/sites", cancellationToken);

    public Task<List<WebSiteCategoryModel>> GetWebSiteCategoriesAsync(CancellationToken cancellationToken = default) =>
        GetAsync<List<WebSiteCategoryModel>>("api/webdata/categories", cancellationToken);

    public Task<WebSiteCategoryModel> CreateWebSiteCategoryAsync(WebSiteCategoryModel data) =>
        PostAsync<WebSiteCategoryModel, WebSiteCategoryModel>("api/webdata/categories", data);

    public Task UpdateWebSiteCategoryAsync(WebSiteCategoryModel data) =>
        PutAsync<WebSiteCategoryModel>($"api/webdata/categories/{data.ID}", data);

    public Task DeleteWebSiteCategoryAsync(int id) =>
        DeleteAsync($"api/webdata/categories/{id}");

    public Task<WebSiteModel?> GetWebSiteAsync(int id) =>
        GetAsync<WebSiteModel?>($"api/webdata/site/{id}");

    public Task<WebSiteModel?> GetWebSiteByDomainAsync(string domain) =>
        GetAsync<WebSiteModel?>($"api/webdata/site-by-domain?domain={HttpUtility.UrlEncode(domain)}");

    public Task<WebSiteModel?> UpdateWebSiteAsync(WebSiteModel website) =>
        PutAsync<WebSiteModel?, WebSiteModel>($"api/webdata/sites/{website.ID}", website);

    public Task UpdateWebSitesCategoryAsync(int[] siteIds, int categoryId) =>
        PostAsync<UpdateSitesCategoryRequest>("api/webdata/update-sites-category", new UpdateSitesCategoryRequest { SiteIds = siteIds, CategoryId = categoryId });

    public Task<List<WebSiteModel>> GetUnSetCategoryWebSitesAsync(CancellationToken cancellationToken = default) =>
        GetAsync<List<WebSiteModel>>("api/webdata/unset-category-sites", cancellationToken);

    public Task ClearWebDataAsync(DateTime? start = null, DateTime? end = null, int? siteId = null)
    {
        var query = new List<string>();
        if (start.HasValue) query.Add($"start={Uri.EscapeDataString(start.Value.ToString("yyyy-MM-ddTHH:mm:ss"))}");
        if (end.HasValue) query.Add($"end={Uri.EscapeDataString(end.Value.ToString("yyyy-MM-ddTHH:mm:ss"))}");
        if (siteId.HasValue) query.Add($"siteId={siteId}");
        var qs = query.Count > 0 ? "?" + string.Join("&", query) : string.Empty;
        return DeleteAsync(TzQuery($"api/webdata/clear{qs}"));
    }

    public Task<List<WebSiteModel>> GetDateRangeWebSiteListAsync(DateTime start, DateTime end, int take = 0, int skip = -1, bool isTime = false, CancellationToken cancellationToken = default) =>
        GetAsync<List<WebSiteModel>>(TzQuery($"api/webdata/range?start={Uri.EscapeDataString(start.ToString("yyyy-MM-ddTHH:mm:ss"))}&end={Uri.EscapeDataString(end.ToString("yyyy-MM-ddTHH:mm:ss"))}&take={take}&skip={skip}&isTime={isTime}"), cancellationToken);

    public Task<int> GetWebSitesCountAsync(int categoryId) =>
        GetAsync<int>($"api/webdata/sites-count?categoryId={categoryId}");

    public Task<List<InfrastructureDataModel>> GetCategoriesStatisticsAsync(DateTime start, DateTime end, CancellationToken cancellationToken = default) =>
        GetAsync<List<InfrastructureDataModel>>(TzQuery($"api/webdata/categories-statistics?start={Uri.EscapeDataString(start.ToString("yyyy-MM-ddTHH:mm:ss"))}&end={Uri.EscapeDataString(end.ToString("yyyy-MM-ddTHH:mm:ss"))}"), cancellationToken);

    public Task<List<InfrastructureDataModel>> GetBrowseDataStatisticsAsync(DateTime start, DateTime end, int siteId = 0, CancellationToken cancellationToken = default) =>
        GetAsync<List<InfrastructureDataModel>>(TzQuery($"api/webdata/browse-statistics?start={Uri.EscapeDataString(start.ToString("yyyy-MM-ddTHH:mm:ss"))}&end={Uri.EscapeDataString(end.ToString("yyyy-MM-ddTHH:mm:ss"))}&siteId={siteId}"), cancellationToken);

    public Task<List<ColumnDataModel>> GetBrowseDataByCategoryStatisticsAsync(DateTime start, DateTime end, CancellationToken cancellationToken = default) =>
        GetAsync<List<ColumnDataModel>>(TzQuery($"api/webdata/browse-category-statistics?start={Uri.EscapeDataString(start.ToString("yyyy-MM-ddTHH:mm:ss"))}&end={Uri.EscapeDataString(end.ToString("yyyy-MM-ddTHH:mm:ss"))}"), cancellationToken);

    public Task<int> GetBrowseDurationTotalAsync(DateTime start, DateTime end, CancellationToken cancellationToken = default) =>
        GetAsync<int>(TzQuery($"api/webdata/browse-duration-total?start={Uri.EscapeDataString(start.ToString("yyyy-MM-ddTHH:mm:ss"))}&end={Uri.EscapeDataString(end.ToString("yyyy-MM-ddTHH:mm:ss"))}"), cancellationToken);

    public Task<int> GetBrowseSitesTotalAsync(DateTime start, DateTime end, CancellationToken cancellationToken = default) =>
        GetAsync<int>(TzQuery($"api/webdata/browse-sites-total?start={Uri.EscapeDataString(start.ToString("yyyy-MM-ddTHH:mm:ss"))}&end={Uri.EscapeDataString(end.ToString("yyyy-MM-ddTHH:mm:ss"))}"), cancellationToken);

    public Task<int> GetBrowsePagesTotalAsync(DateTime start, DateTime end, CancellationToken cancellationToken = default) =>
        GetAsync<int>(TzQuery($"api/webdata/browse-pages-total?start={Uri.EscapeDataString(start.ToString("yyyy-MM-ddTHH:mm:ss"))}&end={Uri.EscapeDataString(end.ToString("yyyy-MM-ddTHH:mm:ss"))}"), cancellationToken);

    public Task<List<WebBrowseLogModel>> GetBrowseLogListAsync(DateTime start, DateTime end, int siteId = 0, CancellationToken cancellationToken = default) =>
        GetAsync<List<WebBrowseLogModel>>(TzQuery($"api/webdata/browse-log-list?start={Uri.EscapeDataString(start.ToString("yyyy-MM-ddTHH:mm:ss"))}&end={Uri.EscapeDataString(end.ToString("yyyy-MM-ddTHH:mm:ss"))}&siteId={siteId}"), cancellationToken);

    public Task<List<WebSiteModel>> GetWebSiteLogListAsync(DateTime start, DateTime end, CancellationToken cancellationToken = default) =>
        GetAsync<List<WebSiteModel>>(TzQuery($"api/webdata/site-log-list?start={Uri.EscapeDataString(start.ToString("yyyy-MM-ddTHH:mm:ss"))}&end={Uri.EscapeDataString(end.ToString("yyyy-MM-ddTHH:mm:ss"))}"), cancellationToken);

    public Task<WebExportDataResult> GetWebExportDataAsync(DateTime start, DateTime end) =>
        GetAsync<WebExportDataResult>(TzQuery($"api/webdata/export?start={Uri.EscapeDataString(start.ToString("yyyy-MM-ddTHH:mm:ss"))}&end={Uri.EscapeDataString(end.ToString("yyyy-MM-ddTHH:mm:ss"))}"));

}

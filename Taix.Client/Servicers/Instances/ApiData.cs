using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Taix.Client.Librarys.Api;
using Taix.Client.Shared.Helpers;
using Taix.Client.Shared.Librarys;
using Taix.Client.Shared.Models;
using Taix.Client.Shared.Models.Data;
using Taix.Client.Shared.Servicers.Interfaces;

namespace Taix.Client.Servicers.Instances;

public class ApiData : IData
{
    private readonly ITaixApiClient _apiClient;

    public ApiData(ITaixApiClient apiClient)
    {
        _apiClient = apiClient;
    }


    public Task UpdateAppDurationAsync(string processName, int duration, DateTime startDateTime)
    {
        return _apiClient.UpdateAppDurationAsync(processName, duration, startDateTime);
    }

    public async Task<IEnumerable<DailyLogModel>> GetDateRangelogListAsync(DateTime start, DateTime end, int take = -1, int skip = -1, CancellationToken cancellationToken = default)
    {
        return await _apiClient.GetDateRangeLogListAsync(start, end, take, skip, cancellationToken);
    }

    public async Task<IEnumerable<DailyLogModel>> GetThisWeeklogListAsync(CancellationToken cancellationToken = default)
    {
        var week = Time.GetThisWeekDate();
        return await GetDateRangelogListAsync(week[0], week[1], cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<DailyLogModel>> GetProcessMonthLogListAsync(int appId, DateTime month, CancellationToken cancellationToken = default)
    {
        var result = await _apiClient.GetProcessMonthLogListAsync(appId, month, cancellationToken);
        return result.AsReadOnly();
    }

    public Task ClearAsync(int appId, DateTime month)
    {
        return _apiClient.ClearAppDataAsync(appId, month);
    }

    public async Task<IReadOnlyList<ColumnDataModel>> GetCategoryHoursDataAsync(DateTime date, CancellationToken cancellationToken = default)
    {
        var result = await _apiClient.GetCategoryHoursDataAsync(date, cancellationToken);
        return result.AsReadOnly();
    }

    public async Task<IReadOnlyList<ColumnDataModel>> GetCategoryRangeDataAsync(DateTime start, DateTime end, CancellationToken cancellationToken = default)
    {
        var result = await _apiClient.GetCategoryRangeDataAsync(start, end, cancellationToken);
        return result.AsReadOnly();
    }

    public async Task<IReadOnlyList<ColumnDataModel>> GetCategoryYearDataAsync(DateTime date, CancellationToken cancellationToken = default)
    {
        var result = await _apiClient.GetCategoryYearDataAsync(date, cancellationToken);
        return result.AsReadOnly();
    }

    public async Task<IReadOnlyList<ColumnDataModel>> GetAppDayDataAsync(int appId, DateTime date, CancellationToken cancellationToken = default)
    {
        var result = await _apiClient.GetAppDayDataAsync(appId, date, cancellationToken);
        return result.AsReadOnly();
    }

    public async Task<IReadOnlyList<ColumnDataModel>> GetAppRangeDataAsync(int appId, DateTime start, DateTime end, CancellationToken cancellationToken = default)
    {
        var result = await _apiClient.GetAppRangeDataAsync(appId, start, end, cancellationToken);
        return result.AsReadOnly();
    }

    public async Task<IReadOnlyList<ColumnDataModel>> GetAppYearDataAsync(int appId, DateTime date, CancellationToken cancellationToken = default)
    {
        var result = await _apiClient.GetAppYearDataAsync(appId, date, cancellationToken);
        return result.AsReadOnly();
    }

    public Task ClearRangeAsync(DateTime start, DateTime end) =>
        _apiClient.ClearRangeAsync(start, end);

    public async Task ExportToExcelAsync(string dir, DateTime start, DateTime end)
    {
        var data = await _apiClient.GetExportDataAsync(start, end);
        var prefix = "Taix";
        var uncategorized = ResourceStrings.Uncategorized;

        var dailyRows = data.DailyLogs.Select(log => new DailyLogExportRow
        {
            Date = log.Date.ToString("yyyy-MM-dd"),
            App = log.AppModel?.Alias ?? log.AppModel?.Name ?? string.Empty,
            Description = log.AppModel?.Description ?? string.Empty,
            Duration = FormatDuration(log.Time),
            Category = log.AppModel?.Category?.Name ?? uncategorized
        });

        var totalSeconds = data.DailyLogs.Sum(l => l.Time);

        var summaryRows = data.DailyLogs
            .GroupBy(l => l.AppModelID)
            .Select(g => new { AppModel = g.First().AppModel, Time = g.Sum(l => l.Time) })
            .OrderByDescending(g => g.Time)
            .Select(g => new AppSummaryExportRow
            {
                App = g.AppModel?.Alias ?? g.AppModel?.Name ?? string.Empty,
                Description = g.AppModel?.Description ?? string.Empty,
                TotalDuration = FormatDuration(g.Time),
                Category = g.AppModel?.Category?.Name ?? uncategorized,
                Percentage = totalSeconds > 0 ? $"{(g.Time * 100.0 / totalSeconds):F2}%" : "0.00%"
            });

        var dailySummaryRows = data.DailyLogs
            .GroupBy(l => l.Date.ToString("yyyy-MM-dd"))
            .Select(g => new { Date = g.Key, Time = g.Sum(l => l.Time) })
            .OrderBy(g => g.Date)
            .Select(g => new DailySummaryExportRow
            {
                Date = g.Date,
                TotalDuration = FormatDuration(g.Time)
            });

        var rangePart = $"{start.ToString("yyyyMMdd")}_{end.ToString("yyyyMMdd")}";
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

        // CSV: daily
        var dailyCsvPath = Path.Combine(dir, $"{prefix}_application_daily_{rangePart}_{timestamp}.csv");
        CsvHelper.WriteCsv(dailyCsvPath, dailyRows, r => $"{CsvHelper.EscapeCsv(r.Date)},{CsvHelper.EscapeCsv(r.App)},{CsvHelper.EscapeCsv(r.Description)},{CsvHelper.EscapeCsv(r.Duration)},{CsvHelper.EscapeCsv(r.Category)}",
            "Date,App,Description,Duration,Category");

        // CSV: summary
        var summaryCsvPath = Path.Combine(dir, $"{prefix}_application_summary_{rangePart}_{timestamp}.csv");
        CsvHelper.WriteCsv(summaryCsvPath, summaryRows, r => $"{CsvHelper.EscapeCsv(r.App)},{CsvHelper.EscapeCsv(r.Description)},{CsvHelper.EscapeCsv(r.TotalDuration)},{CsvHelper.EscapeCsv(r.Category)},{CsvHelper.EscapeCsv(r.Percentage)}",
            "App,Description,TotalDuration,Category,Percentage");
    }

    private static string FormatDuration(int seconds)
    {
        var ts = TimeSpan.FromSeconds(seconds);
        return $"{ts.Hours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}";
    }

    public async Task<int> GetDateRangeAppCountAsync(DateTime start, DateTime end, CancellationToken cancellationToken = default)
    {
        return await _apiClient.GetDateRangeAppCountAsync(start, end, cancellationToken);
    }

    public async Task<IEnumerable<HoursLogModel>> GetTimeRangelogListAsync(DateTime time, CancellationToken cancellationToken = default)
    {
        return await _apiClient.GetTimeRangeLogListAsync(time, cancellationToken);
    }

    public async Task<IEnumerable<HoursLogModel>> GetHoursRangeLogListAsync(DateTime start, DateTime end, CancellationToken cancellationToken = default)
    {
        return await _apiClient.GetHoursRangeLogListAsync(start, end, cancellationToken);
    }

    public async Task<IEnumerable<AppSessionModel>> GetAppSessionsAsync(DateTime start, DateTime end, CancellationToken cancellationToken = default)
    {
        return await _apiClient.GetAppSessionsAsync(start, end, cancellationToken);
    }

    public async Task<double[]> GetRangeTotalDataAsync(DateTime start, DateTime end, CancellationToken cancellationToken = default)
    {
        return await _apiClient.GetRangeTotalDataAsync(start, end, cancellationToken);
    }

    public async Task<double[]> GetMonthTotalDataAsync(DateTime year, CancellationToken cancellationToken = default)
    {
        return await _apiClient.GetMonthTotalDataAsync(year, cancellationToken);
    }

    public Task ClearAsync(int appId)
    {
        return _apiClient.ClearAppDataAsync(appId);
    }
}

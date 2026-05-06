using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MiniExcelLibs;
using Taix.Client.Librarys.Api;
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

    public async Task<IReadOnlyList<DailyLogModel>> GetTodaylogListAsync()
    {
        var result = await _apiClient.GetTodayLogListAsync();
        return result.AsReadOnly();
    }

    public async Task<IEnumerable<DailyLogModel>> GetDateRangelogListAsync(DateTime start, DateTime end, int take = -1, int skip = -1, CancellationToken cancellationToken = default)
    {
        return await _apiClient.GetDateRangeLogListAsync(start, end, take, skip);
    }

    public async Task<IEnumerable<DailyLogModel>> GetThisWeeklogListAsync()
    {
        var week = Time.GetThisWeekDate();
        return await GetDateRangelogListAsync(week[0], week[1]);
    }

    public async Task<IEnumerable<DailyLogModel>> GetLastWeeklogListAsync()
    {
        return await _apiClient.GetLastWeekLogListAsync();
    }

    public async Task<IReadOnlyList<DailyLogModel>> GetProcessMonthLogListAsync(int appId, DateTime month)
    {
        var result = await _apiClient.GetProcessMonthLogListAsync(appId, month);
        return result.AsReadOnly();
    }

    public Task ClearAsync(int appId, DateTime month)
    {
        return _apiClient.ClearAppDataAsync(appId, month);
    }

    public async Task<DailyLogModel> GetProcessAsync(int appId, DateTime day)
    {
        var result = await _apiClient.GetProcessDayAsync(appId, day);
        return result!;
    }

    public async Task<IReadOnlyList<ColumnDataModel>> GetCategoryHoursDataAsync(DateTime date, CancellationToken cancellationToken = default)
    {
        var result = await _apiClient.GetCategoryHoursDataAsync(date);
        return result.AsReadOnly();
    }

    public async Task<IReadOnlyList<ColumnDataModel>> GetCategoryRangeDataAsync(DateTime start, DateTime end, CancellationToken cancellationToken = default)
    {
        var result = await _apiClient.GetCategoryRangeDataAsync(start, end);
        return result.AsReadOnly();
    }

    public async Task<IReadOnlyList<ColumnDataModel>> GetCategoryYearDataAsync(DateTime date, CancellationToken cancellationToken = default)
    {
        var result = await _apiClient.GetCategoryYearDataAsync(date);
        return result.AsReadOnly();
    }

    public async Task<IReadOnlyList<ColumnDataModel>> GetAppDayDataAsync(int appId, DateTime date)
    {
        var result = await _apiClient.GetAppDayDataAsync(appId, date);
        return result.AsReadOnly();
    }

    public async Task<IReadOnlyList<ColumnDataModel>> GetAppRangeDataAsync(int appId, DateTime start, DateTime end)
    {
        var result = await _apiClient.GetAppRangeDataAsync(appId, start, end);
        return result.AsReadOnly();
    }

    public async Task<IReadOnlyList<ColumnDataModel>> GetAppYearDataAsync(int appId, DateTime date)
    {
        var result = await _apiClient.GetAppYearDataAsync(appId, date);
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

        var excelPath = Path.Combine(dir, $"{prefix}_application_data_{rangePart}_{timestamp}.xlsx");
        var sheets = new Dictionary<string, object>
        {
            [ResourceStrings.ExportDaily] = dailyRows,
            [ResourceStrings.ExportSummary] = summaryRows,
            [ResourceStrings.ExportDailySummary] = dailySummaryRows
        };
        MiniExcel.SaveAs(excelPath, sheets);

        // CSV: daily
        var dailyCsvPath = Path.Combine(dir, $"{prefix}_application_daily_{rangePart}_{timestamp}.csv");
        MiniExcel.SaveAs(dailyCsvPath, dailyRows, excelType: MiniExcelLibs.ExcelType.CSV);

        // CSV: summary
        var summaryCsvPath = Path.Combine(dir, $"{prefix}_application_summary_{rangePart}_{timestamp}.csv");
        MiniExcel.SaveAs(summaryCsvPath, summaryRows, excelType: MiniExcelLibs.ExcelType.CSV);
    }

    private static string FormatDuration(int seconds)
    {
        var ts = TimeSpan.FromSeconds(seconds);
        return $"{ts.Hours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}";
    }

    public async Task<int> GetDateRangeAppCountAsync(DateTime start, DateTime end, CancellationToken cancellationToken = default)
    {
        return await _apiClient.GetDateRangeAppCountAsync(start, end);
    }

    public async Task<IEnumerable<HoursLogModel>> GetTimeRangelogListAsync(DateTime time, CancellationToken cancellationToken = default)
    {
        return await _apiClient.GetTimeRangeLogListAsync(time);
    }

    public async Task<double[]> GetRangeTotalDataAsync(DateTime start, DateTime end, CancellationToken cancellationToken = default)
    {
        return await _apiClient.GetRangeTotalDataAsync(start, end);
    }

    public async Task<double[]> GetMonthTotalDataAsync(DateTime year, CancellationToken cancellationToken = default)
    {
        return await _apiClient.GetMonthTotalDataAsync(year);
    }

    public Task ClearAsync(int appId)
    {
        return _apiClient.ClearAppDataAsync(appId);
    }
}

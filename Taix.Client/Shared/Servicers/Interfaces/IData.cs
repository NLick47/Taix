using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Taix.Client.Shared.Models;
using Taix.Client.Shared.Models.Data;

namespace Taix.Client.Shared.Servicers.Interfaces;

public interface IData
{
    /// <summary>
    /// 查询指定范围数据
    /// </summary>
    /// <param name="start"></param>
    /// <param name="end"></param>
    /// <returns></returns>
    Task<IEnumerable<DailyLogModel>> GetDateRangelogListAsync(DateTime start, DateTime end, int take = -1,
        int skip = -1, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取本周的数据
    /// </summary>
    /// <returns></returns>
    Task<IEnumerable<DailyLogModel>> GetThisWeeklogListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取指定应用某个月的数据
    /// </summary>
    /// <param name="appId"></param>
    /// <param name="month"></param>
    /// <returns></returns>
    Task<IReadOnlyList<DailyLogModel>> GetProcessMonthLogListAsync(int appId, DateTime month, CancellationToken cancellationToken = default);

    /// <summary>
    /// 清空指定应用某月的数据
    /// </summary>
    /// <param name="appId"></param>
    Task ClearAsync(int appId, DateTime month);

    Task<IReadOnlyList<ColumnDataModel>> GetAppDayDataAsync(int appId, DateTime date, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ColumnDataModel>> GetAppRangeDataAsync(int appId, DateTime start, DateTime end, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ColumnDataModel>> GetAppYearDataAsync(int appId, DateTime date, CancellationToken cancellationToken = default);

    /// <summary>
    /// 清空指定时间范围数据
    /// </summary>
    /// <param name="start"></param>
    /// <param name="end"></param>
    Task ClearRangeAsync(DateTime start, DateTime end);

    /// <summary>
    /// 导出数据到Excel/CSV
    /// </summary>
    Task ExportToExcelAsync(string dir, DateTime start, DateTime end);

    /// <summary>
    /// 获取指定时间（小时）所有使用app数据
    /// </summary>
    /// <param name="time"></param>
    /// <returns></returns>
    Task<IEnumerable<HoursLogModel>> GetTimeRangelogListAsync(DateTime time, CancellationToken cancellationToken = default);

    /// <summary>
    /// 清空所有统计数据
    /// </summary>
    /// <param name="appId">应用ID</param>
    Task ClearAsync(int appId);

    Task<OverviewBundleModel> GetOverviewBundleAsync(DateTime start, DateTime end, int take = 0, bool withColumns = false, CancellationToken cancellationToken = default);

    Task<ChartBundleModel> GetChartBundleAsync(DateTime start, DateTime end, DateTime? prevStart, DateTime? prevEnd, string bucket = "hour", int take = 5, CancellationToken cancellationToken = default);

    Task<DayDetailBundleModel> GetDayDetailBundleAsync(DateTime date, CancellationToken cancellationToken = default);
}

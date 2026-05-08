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
    /// 保存APP使用时长数据
    /// </summary>
    /// <param name="processName">进程名称</param>
    /// <param name="duration">时长（秒）</param>
    /// <param name="startDateTime">记录开始时间</param>
    Task UpdateAppDurationAsync(string processName, int duration, DateTime startDateTime);

    /// <summary>
    /// 查询指定范围数据
    /// </summary>
    /// <param name="start"></param>
    /// <param name="end"></param>
    /// <returns></returns>
    Task<IEnumerable<DailyLogModel>> GetDateRangelogListAsync(DateTime start, DateTime end, int take = -1,
        int skip = -1, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取概览页置顶数据（服务端根据配置自动分页）
    /// </summary>

    /// <summary>
    /// 获取本周的数据
    /// </summary>
    /// <returns></returns>
    Task<IEnumerable<DailyLogModel>> GetThisWeeklogListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取指定进程某个月的数据
    /// </summary>
    /// <param name="processName"></param>
    /// <param name="month"></param>
    /// <returns></returns>
    Task<IReadOnlyList<DailyLogModel>> GetProcessMonthLogListAsync(int appId, DateTime month, CancellationToken cancellationToken = default);


    /// <summary>
    /// 清空指定进程某月的数据
    /// </summary>
    /// <param name="processName"></param>
    Task ClearAsync(int appId, DateTime month);

    /// <summary>
    /// 获取指定日期所有分类时段统计数据
    /// </summary>
    /// <returns></returns>
    Task<IReadOnlyList<ColumnDataModel>> GetCategoryHoursDataAsync(DateTime date, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取指定日期范围所有分类按天统计数据
    /// </summary>
    /// <param name="start"></param>
    /// <param name="end"></param>
    /// <returns></returns>
    Task<IReadOnlyList<ColumnDataModel>> GetCategoryRangeDataAsync(DateTime start, DateTime end, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取指定年份所有分类按月份统计数据
    /// </summary>
    /// <param name="date"></param>
    /// <returns></returns>
    Task<IReadOnlyList<ColumnDataModel>> GetCategoryYearDataAsync(DateTime date, CancellationToken cancellationToken = default);

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
    /// 获取指定日期范围使用应用量
    /// </summary>
    /// <param name="start"></param>
    /// <param name="end"></param>
    /// <returns></returns>
    Task<int> GetDateRangeAppCountAsync(DateTime start, DateTime end, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取指定时间（小时）所有使用app数据
    /// </summary>
    /// <param name="time"></param>
    /// <returns></returns>
    Task<IEnumerable<HoursLogModel>> GetTimeRangelogListAsync(DateTime time, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取指定时间范围内的汇总数据
    /// </summary>
    /// <param name="start"></param>
    /// <param name="end"></param>
    /// <returns></returns>
    Task<double[]> GetRangeTotalDataAsync(DateTime start, DateTime end, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取指定年份按月统计数据
    /// </summary>
    /// <param name="year"></param>
    /// <returns></returns>
    Task<double[]> GetMonthTotalDataAsync(DateTime year, CancellationToken cancellationToken = default);

    /// <summary>
    /// 清空所有统计数据
    /// </summary>
    /// <param name="appId">应用ID</param>
    Task ClearAsync(int appId);
}

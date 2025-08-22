using Core.Models;
using Core.Models.Data;

namespace Core.Servicers.Interfaces;

public interface IData
{
    /// <summary>
    ///     保存APP使用时长数据
    /// </summary>
    /// <param name="processName_">进程名称</param>
    /// <param name="duration_">时长（秒）</param>
    /// <param name="startDateTime_">记录开始时间</param>
    Task UpdateAppDurationAsync(string processName_, int duration_, DateTime startDateTime_);

    /// <summary>
    ///     获取今天的数据
    /// </summary>
    /// <returns></returns>
    Task<IReadOnlyList<DailyLogModel>> GetTodaylogListAsync();

    /// <summary>
    ///     查询指定范围数据
    /// </summary>
    /// <param name="start"></param>
    /// <param name="end"></param>
    /// <returns></returns>
    Task<IEnumerable<DailyLogModel>> GetDateRangelogListAsync(DateTime start, DateTime end, int take = -1,
        int skip = -1);

    /// <summary>
    ///     获取本周的数据
    /// </summary>
    /// <returns></returns>
    Task<IEnumerable<DailyLogModel>> GetThisWeeklogListAsync();

    /// <summary>
    ///     获取上周的数据
    /// </summary>
    /// <returns></returns>
    Task<IEnumerable<DailyLogModel>> GetLastWeeklogListAsync();

    /// <summary>
    ///     获取指定进程某个月的数据
    /// </summary>
    /// <param name="processName"></param>
    /// <param name="month"></param>
    /// <returns></returns>
    Task<IReadOnlyList<DailyLogModel>> GetProcessMonthLogListAsync(int appID, DateTime month);


    /// <summary>
    ///     清空指定进程某月的数据
    /// </summary>
    /// <param name="processName"></param>
    Task ClearAsync(int appID, DateTime month);

    /// <summary>
    ///     获取指定进程某天的数据
    /// </summary>
    /// <param name="processName"></param>
    /// <param name="day"></param>
    /// <returns></returns>
    Task<DailyLogModel> GetProcessAsync(int appID, DateTime day);

    /// <summary>
    ///     获取指定日期所有分类时段统计数据
    /// </summary>
    /// <returns></returns>
    Task<IReadOnlyList<ColumnDataModel>> GetCategoryHoursDataAsync(DateTime date);

    /// <summary>
    ///     获取指定日期范围所有分类按天统计数据
    /// </summary>
    /// <param name="start"></param>
    /// <param name="end"></param>
    /// <returns></returns>
    Task<IReadOnlyList<ColumnDataModel>> GetCategoryRangeDataAsync(DateTime start, DateTime end);

    /// <summary>
    ///     获取指定年份所有分类按月份统计数据
    /// </summary>
    /// <param name="date"></param>
    /// <returns></returns>
    Task<IReadOnlyList<ColumnDataModel>> GetCategoryYearDataAsync(DateTime date);

    Task<IReadOnlyList<ColumnDataModel>> GetAppDayDataAsync(int appID, DateTime date);
    Task<IReadOnlyList<ColumnDataModel>> GetAppRangeDataAsync(int appID, DateTime start, DateTime end);
    Task<IReadOnlyList<ColumnDataModel>> GetAppYearDataAsync(int appID, DateTime date);

    /// <summary>
    ///     清空指定时间范围数据
    /// </summary>
    /// <param name="start"></param>
    /// <param name="end"></param>
    Task ClearRangeAsync(DateTime start, DateTime end);

    /// <summary>
    ///     导出数据到EXCEL
    /// </summary>
    Task ExportToExcelAsync(string dir,
        DateTime start,
        DateTime end,
        ExportOptions options);

    /// <summary>
    ///     获取指定日期范围使用应用量
    /// </summary>
    /// <param name="start"></param>
    /// <param name="end"></param>
    /// <returns></returns>
    Task<int> GetDateRangeAppCountAsync(DateTime start, DateTime end);

    /// <summary>
    ///     获取指定时间（小时）所有使用app数据
    /// </summary>
    /// <param name="time"></param>
    /// <returns></returns>
    Task<IEnumerable<HoursLogModel>> GetTimeRangelogListAsync(DateTime time);

    /// <summary>
    ///     获取指定时间范围内的汇总数据
    /// </summary>
    /// <param name="start"></param>
    /// <param name="end"></param>
    /// <returns></returns>
    Task<double[]> GetRangeTotalDataAsync(DateTime start, DateTime end);

    /// <summary>
    ///     获取指定年份按月统计数据
    /// </summary>
    /// <param name="year"></param>
    /// <returns></returns>
    Task<double[]> GetMonthTotalDataAsync(DateTime year);

    /// <summary>
    ///     清空所有统计数据
    /// </summary>
    /// <param name="appID_">应用ID</param>
    Task ClearAsync(int appID_);
}
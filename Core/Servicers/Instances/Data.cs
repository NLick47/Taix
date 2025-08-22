using System.Diagnostics;
using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using Core.Librarys;
using Core.Librarys.SQLite;
using Core.Models;
using Core.Models.Data;
using Core.Servicers.Interfaces;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.EntityFrameworkCore;
using SharedLibrary.Librarys;

namespace Core.Servicers.Instances;

public class Data : IData
{
    private readonly IAppData _appData;
    private readonly object setLock = new();

    public Data(IAppData appData_)
    {
        _appData = appData_;
    }

    public async Task UpdateAppDurationAsync(string process_, int duration_, DateTime startTime_)
    {
        //  过滤无效值
        if (string.IsNullOrEmpty(process_) || duration_ <= 0 || startTime_ == DateTime.MinValue) return;

        Logger.Info($"UpdateAppDuration,process:{process_},duration:{duration_},start:{startTime_}");
        //  开始时间剩余最大统计时长
        var startTimeMaxHoursDuration = (59 - startTime_.Minute) * 60 + (60 - startTime_.Second);
        //  开始时间使用时长
        var startTimeHoursDuration = duration_ > startTimeMaxHoursDuration ? startTimeMaxHoursDuration : duration_;
        //  剩余时长
        var outHoursDuration = duration_ - startTimeHoursDuration;
        //  结束时间
        var endTime = new DateTime(startTime_.Year, startTime_.Month, startTime_.Day, startTime_.Hour, 0, 0);
        //  时段使用数据
        var durationHoursData = new Dictionary<DateTime, int>
        {
            { endTime, startTimeHoursDuration }
        };
        //  计算时段数据
        if (outHoursDuration > 0)
        {
            var outHours = outHoursDuration / 3600;

            var outStartTime = new DateTime(startTime_.Year, startTime_.Month, startTime_.Day, startTime_.Hour, 0, 0);
            for (var i = 0; i < outHours; i++)
            {
                outStartTime = outStartTime.AddHours(1);
                var duration = 3600;
                durationHoursData.Add(outStartTime, duration);
            }

            if (outHoursDuration % 3600 > 0)
            {
                outStartTime = outStartTime.AddHours(1);
                var duration = outHoursDuration - outHours * 3600;
                durationHoursData.Add(outStartTime, duration);
            }

            endTime = outStartTime;
        }

        //  计算每日统计数据
        var nextDayTime = new DateTime(startTime_.Year, startTime_.Month, startTime_.Day, 0, 0, 0).AddDays(1);
        var startTimeMaxDayDuration = (int)(nextDayTime - startTime_).TotalSeconds;
        //  开始时间使用时长
        var startTimeDayDuration = duration_ > startTimeMaxDayDuration ? startTimeMaxDayDuration : duration_;
        //  剩余时长
        var outDayDuration = duration_ - startTimeDayDuration;
        //  结束时间
        var endDayTime = startTime_.Date;
        //  每日使用数据
        var durationDayData = new Dictionary<DateTime, int>
        {
            { startTime_.Date, startTimeDayDuration }
        };
        if (outDayDuration > 0)
        {
            var outDays = outDayDuration / 86400;

            var outStartTime = new DateTime(startTime_.Year, startTime_.Month, startTime_.Day, 0, 0, 0);
            for (var i = 0; i < outDays; i++)
            {
                outStartTime = outStartTime.AddDays(1);
                var duration = 86400;
                durationDayData.Add(outStartTime, duration);
            }

            if (outDayDuration % 86400 > 0)
            {
                outStartTime = outStartTime.AddDays(1);
                var duration = outDayDuration - outDays * 86400;
                durationDayData.Add(outStartTime, duration);
            }

            endDayTime = outStartTime.Date;
        }

        //  开始写入数据
        try
        {
            var db = new TaiDbContext();
            var app = await db.App.FirstOrDefaultAsync(m => m.Name == process_);
            if (app == null) return;

            //  更新app累计总时长
            app.TotalTime += duration_;

            //  更新每日数据
            var dailyLogs = await db.DailyLog
                .Where(m => m.Date >= startTime_.Date && m.Date <= endDayTime && m.AppModelID == app.ID)
                .ToListAsync();
            foreach (var item in durationDayData)
            {
                var log = dailyLogs.FirstOrDefault(m => m.Date == item.Key);
                if (log == null)
                {
                    //数据库中没有时则创建
                    db.DailyLog.Add(new DailyLogModel
                    {
                        Date = item.Key,
                        AppModelID = app.ID,
                        Time = item.Value
                    });
                }
                else
                {
                    var time = log.Time + item.Value;
                    log.Time = time > 86400 ? 86400 : time;
                }
            }

            //  更新时段数据
            var startDataTime = new DateTime(startTime_.Year, startTime_.Month, startTime_.Day, startTime_.Hour, 0, 0);
            var hoursLogs = await db.HoursLog
                .Where(m => m.DataTime >= startDataTime && startDataTime <= endTime && m.AppModelID == app.ID)
                .ToListAsync();
            foreach (var item in durationHoursData)
            {
                var log = hoursLogs.FirstOrDefault(m => startDataTime == item.Key);
                if (log == null)
                {
                    //  没有记录时创建
                    db.HoursLog.Add(new HoursLogModel
                    {
                        DataTime = item.Key,
                        AppModelID = app.ID,
                        Time = item.Value
                    });
                }
                else
                {
                    var time = log.Time + item.Value;
                    log.Time = time > 3600 ? 3600 : time;
                }
            }

            await db.SaveChangesAsync();
        }
        catch (Exception e)
        {
            Logger.Error(
                $"UpdateAppDuration error!Process:{process_},Duration:{duration_},StartDateTime:{startTime_}.\r\nError:\r\n{e.Message}");
        }
    }

    public async Task<IReadOnlyList<DailyLogModel>> GetTodaylogListAsync()
    {
        using var db = new TaiDbContext();
        var today = DateTime.Now.Date;
        var res = db.DailyLog.Where(m => m.Date == today && m.AppModelID != 0);
        return (await res.ToListAsync()).AsReadOnly();
    }

    public async Task<IEnumerable<DailyLogModel>> GetDateRangelogListAsync(DateTime start, DateTime end, int take = -1,
        int skip = -1)
    {
        IEnumerable<AppModel> apps = _appData.GetAllApps();
        using var db = new TaiDbContext();
        var data = db.DailyLog
            .Where(m => m.Date >= start && m.Date <= end && m.AppModelID != 0)
            .GroupBy(m => m.AppModelID)
            .Select(m => new
            {
                Time = m.Sum(a => a.Time),
                m.FirstOrDefault().Date,
                AppID = m.FirstOrDefault().AppModelID
            });
        if (skip > 0 && take > 0)
            data = data.OrderByDescending(m => m.Time).Skip(skip).Take(take);
        else if (skip > 0)
            data = data.OrderByDescending(m => m.Time).Skip(skip);
        else if (take > 0)
            data = data.OrderByDescending(m => m.Time).Take(take);
        else
            data = data.OrderByDescending(m => m.Time);
        var res = await data
            .Select(m => new DailyLogModel
            {
                Time = m.Time,
                Date = m.Date,
                AppModelID = m.AppID,
                AppModel = _appData.GetApp(m.AppID)
            }).ToListAsync();

        return res;
    }

    public Task<IEnumerable<DailyLogModel>> GetThisWeeklogListAsync()
    {
        DateTime weekStartDate = DateTime.Now, weekEndDate = DateTime.Now;
        if (DateTime.Now.DayOfWeek == DayOfWeek.Monday)
        {
            weekStartDate = DateTime.Now.Date;
            weekEndDate = DateTime.Now.Date.AddDays(6);
        }
        else
        {
            var weekNum = (int)DateTime.Now.DayOfWeek;
            if (weekNum == 0) weekNum = 7;
            weekNum -= 1;
            weekStartDate = DateTime.Now.Date.AddDays(-weekNum);
            weekEndDate = weekStartDate.Date.AddDays(6);
        }

        return GetDateRangelogListAsync(weekStartDate, weekEndDate);
    }

    public Task<IEnumerable<DailyLogModel>> GetLastWeeklogListAsync()
    {
        DateTime weekStartDate = DateTime.Now, weekEndDate = DateTime.Now;

        var weekNum = (int)DateTime.Now.DayOfWeek;
        if (weekNum == 0) weekNum = 7;
        weekStartDate = DateTime.Now.Date.AddDays(-6 - weekNum);
        weekEndDate = weekStartDate.AddDays(6);

        return GetDateRangelogListAsync(weekStartDate, weekEndDate);
    }

    public async Task<IReadOnlyList<DailyLogModel>> GetProcessMonthLogListAsync(int appID, DateTime month)
    {
        using var db = new TaiDbContext();
        var res = db.DailyLog.Include(m => m.AppModel).Where(
            m =>
                m.Date.Year == month.Year
                && m.Date.Month == month.Month
                && m.AppModelID == appID
        );
        if (res != null) return (await res.ToListAsync()).AsReadOnly();
        return Array.AsReadOnly(Array.Empty<DailyLogModel>());
    }


    public async Task ClearAsync(int appID, DateTime month)
    {
        using var db = new TaiDbContext();
        db.DailyLog.RemoveRange(
            db.DailyLog.Where(m =>
                m.AppModelID == appID
                && m.Date.Year == month.Year
                && m.Date.Month == month.Month));

        db.HoursLog.RemoveRange(
            db.HoursLog.Where(m => m.AppModelID == appID
                                   && m.DataTime.Year == month.Year
                                   && m.DataTime.Month == month.Month));
        await db.SaveChangesAsync();
    }

    public Task<DailyLogModel> GetProcessAsync(int appID, DateTime day)
    {
        var db = new TaiDbContext();
        var res = db.DailyLog.Where(m =>
            m.AppModelID == appID
            && m.Date.Year == day.Year
            && m.Date.Month == day.Month
            && m.Date.Day == day.Day);
        if (res != null) return res.FirstOrDefaultAsync();
        return null;
    }

    public async Task<IReadOnlyList<ColumnDataModel>> GetCategoryHoursDataAsync(DateTime date)
    {
        var db = new TaiDbContext();
        //  查出有数据的分类
        var startDate = date.Date;
        var endDate = startDate.AddDays(1).AddSeconds(-1);
        var categorys = await (from hoursLog in db.HoursLog
                join app in db.App on hoursLog.AppModelID equals app.ID
                where hoursLog.DataTime >= startDate && hoursLog.DataTime <= endDate
                group hoursLog by app.CategoryID
                into g
                select new CategoryHoursDataModel
                {
                    Total = g.Sum(x => x.Time),
                    CategoryID = g.Key,
                    Time = g.FirstOrDefault().DataTime
                })
            .OrderByDescending(m => m.CategoryID)
            .ToArrayAsync();


        var data = await (from hoursLog in db.HoursLog
                join app in db.App on hoursLog.AppModelID equals app.ID
                where hoursLog.DataTime >= startDate && hoursLog.DataTime <= endDate
                group hoursLog by new { app.CategoryID, hoursLog.DataTime }
                into g
                select new CategoryHoursDataModel
                {
                    Total = g.Sum(x => x.Time),
                    CategoryID = g.Key.CategoryID,
                    Time = g.Key.DataTime
                })
            .ToArrayAsync();

        var list = categorys.Select(c => new ColumnDataModel
        {
            CategoryID = c.CategoryID,
            Values = new double[24]
        }).ToList();

        for (var i = 0; i < 24; i++)
        {
            var hours = i < 10 ? "0" + i : i.ToString();
            var time = date.ToString($"yyyy-MM-dd {hours}:00:00");
            foreach (var category in categorys)
            {
                var log = data.Where(m =>
                        m.CategoryID == category.CategoryID && m.Time.ToString("yyyy-MM-dd HH:00:00") == time)
                    .FirstOrDefault();

                var item = list.Where(m => m.CategoryID == category.CategoryID).FirstOrDefault();

                item.Values[i] = log.Total;
            }
        }

        return list.AsReadOnly();
    }

    public async Task<IReadOnlyList<ColumnDataModel>> GetCategoryRangeDataAsync(DateTime start, DateTime end)
    {
        using var db = new TaiDbContext();
        var categorys = await (from log in db.DailyLog
                join app in db.App on log.AppModelID equals app.ID
                where log.Date >= start.Date && log.Date <= end.Date
                group log by app.CategoryID
                into g
                select new CategoryHoursDataModel
                {
                    Total = g.Sum(log => log.Time),
                    CategoryID = g.Key,
                    Time = g.Max(log => log.Date)
                })
            .OrderByDescending(m => m.CategoryID)
            .ToArrayAsync();

        var data = await (from log in db.DailyLog
                join app in db.App on log.AppModelID equals app.ID
                where log.Date >= start.Date && log.Date <= end.Date
                group log by new { app.CategoryID, log.Date }
                into g
                select new CategoryHoursDataModel
                {
                    Total = g.Sum(log => log.Time),
                    CategoryID = g.Key.CategoryID,
                    Time = g.Key.Date
                })
            .ToArrayAsync();

        var ts = end - start;
        var days = ts.TotalDays + 1;
        var list = categorys.Select(category => new ColumnDataModel
        {
            CategoryID = category.CategoryID,
            Values = new double[(int)days]
        }).ToList();

        for (var i = 0; i < days; i++)
        {
            var day = i < 10 ? "0" + i : i.ToString();
            var time = start.AddDays(i).ToString("yyyy-MM-dd 00:00:00");
            Debug.WriteLine(time);
            foreach (var category in categorys)
            {
                var log = data.Where(m =>
                        m.CategoryID == category.CategoryID && m.Time.ToString("yyyy-MM-dd 00:00:00") == time)
                    .FirstOrDefault();

                var item = list.Where(m => m.CategoryID == category.CategoryID)
                    .FirstOrDefault();

                item.Values[i] = log.Total;
            }
        }

        return list.AsReadOnly();
    }

    public async Task<IReadOnlyList<ColumnDataModel>> GetCategoryYearDataAsync(DateTime date)
    {
        using var db = new TaiDbContext();

        //  查出有数据的分类
        var dateArr = Time.GetYearDate(date);
        var startDate = dateArr[0].Date;
        var endDate = dateArr[1].Date;
        var categorys = await (from dailyLog in db.DailyLog
                join app in db.App on dailyLog.AppModelID equals app.ID
                where dailyLog.Date >= startDate && dailyLog.Date <= endDate
                group dailyLog by app.CategoryID
                into g
                select new CategoryHoursDataModel
                {
                    Total = g.Sum(x => x.Time),
                    CategoryID = g.Key,
                    Time = g.FirstOrDefault().Date
                })
            .OrderByDescending(m => m.CategoryID)
            .ToArrayAsync();


        var data = await (from dailyLog in db.DailyLog
                join app in db.App on dailyLog.AppModelID equals app.ID
                where dailyLog.Date >= startDate && dailyLog.Date <= endDate
                group dailyLog by new { app.CategoryID, dailyLog.Date }
                into g
                select new CategoryHoursDataModel
                {
                    Total = g.Sum(x => x.Time),
                    CategoryID = g.Key.CategoryID,
                    Time = g.Key.Date
                })
            .ToArrayAsync();

        var list = categorys.Select(category => new ColumnDataModel
        {
            CategoryID = category.CategoryID,
            Values = new double[12]
        }).ToList();

        for (var i = 1; i < 13; i++)
        {
            var month = i < 10 ? "0" + i : i.ToString();
            var dayArr = Time.GetMonthDate(new DateTime(date.Year, i, 1));

            Debug.WriteLine(dayArr);
            foreach (var category in categorys)
            {
                var total = data
                    .Where(m => m.CategoryID == category.CategoryID && m.Time >= dayArr[0] && m.Time <= dayArr[1])
                    .Sum(m => m.Total);

                var item = list.Where(m => m.CategoryID == category.CategoryID).FirstOrDefault();

                item.Values[i - 1] = total;
            }
        }

        return list.AsReadOnly();
    }

    public async Task<IReadOnlyList<ColumnDataModel>> GetAppDayDataAsync(int appID, DateTime date)
    {
        using var db = new TaiDbContext();
        var endDate = date.Date.AddDays(1).AddSeconds(-1);
        var startDate = date.Date;
        var data = await db.HoursLog
            .Where(log => log.AppModelID == appID &&
                          log.DataTime >= startDate &&
                          log.DataTime <= endDate)
            .GroupBy(log => log.DataTime)
            .Select(g => new ColumnItemDataModel
            {
                Total = g.Sum(log => log.Time),
                AppID = appID,
                Time = g.Key
            })
            .ToArrayAsync();

        List<ColumnDataModel> list = new()
        {
            new ColumnDataModel
            {
                AppId = appID,
                Values = new double[24]
            }
        };

        var item = list[0];

        for (var i = 0; i < item.Values.Length; i++)
        {
            var hours = i < 10 ? "0" + i : i.ToString();
            var time = date.ToString($"yyyy-MM-dd {hours}:00:00");
            var log = data.FirstOrDefault(m => m.Time.ToString("yyyy-MM-dd HH:00:00") == time);
            item.Values[i] = log.Total;
        }

        return list.AsReadOnly();
    }

    public async Task<IReadOnlyList<ColumnDataModel>> GetAppRangeDataAsync(int appID, DateTime start, DateTime end)
    {
        using var db = new TaiDbContext();
        var data = await db.DailyLog
            .Where(log => log.AppModelID == appID &&
                          log.Date >= start.Date &&
                          log.Date <= end.Date.AddDays(1).AddTicks(-1))
            .GroupBy(log => log.Date)
            .Select(g => new ColumnItemDataModel
            {
                Total = g.Sum(log => log.Time),
                AppID = appID,
                Time = g.Key
            })
            .ToArrayAsync();
        var ts = end - start;
        var days = ts.TotalDays + 1;
        List<ColumnDataModel> list = new()
        {
            new ColumnDataModel
            {
                AppId = appID,
                Values = new double[(int)days]
            }
        };
        var item = list[0];
        for (var i = 0; i < days; i++)
        {
            var time = start.AddDays(i);
            var log = data.Where(m => m.Time == time).FirstOrDefault();
            item.Values[i] = log.Total;
        }

        return list.AsReadOnly();
    }

    public async Task<IReadOnlyList<ColumnDataModel>> GetAppYearDataAsync(int appID, DateTime date)
    {
        using var db = new TaiDbContext();
        //  查出有数据的分类
        var dateArr = Time.GetYearDate(date);

        var startDate = dateArr[0].Date;
        var endDate = dateArr[1].Date;

        var data = await db.DailyLog
            .Where(log => log.AppModelID == appID &&
                          log.Date >= startDate &&
                          log.Date <= endDate)
            .GroupBy(log => log.Date)
            .Select(g => new ColumnItemDataModel
            {
                Total = g.Sum(log => log.Time),
                AppID = g.Select(log => log.AppModelID).FirstOrDefault(),
                Time = g.Key
            })
            .ToArrayAsync();
        List<ColumnDataModel> list = new()
        {
            new ColumnDataModel
            {
                AppId = appID,
                Values = new double[12]
            }
        };
        var item = list[0];

        for (var i = 1; i < 13; i++)
        {
            var month = i < 10 ? "0" + i : i.ToString();
            var dayArr = Time.GetMonthDate(new DateTime(date.Year, i, 1));
            var total = data.Where(m => m.Time >= dayArr[0] && m.Time <= dayArr[1]).Sum(m => m.Total);
            item.Values[i - 1] = total;
        }

        return list.AsReadOnly();
    }

    public async Task ClearRangeAsync(DateTime start, DateTime end)
    {
        end = new DateTime(end.Year, end.Month, DateTime.DaysInMonth(end.Year, end.Month));
        var startDate = start.Date.ToString("yyyy-MM-01 00:00:00");
        var endDate = end.Date.ToString("yyyy-MM-dd 23:59:59");

        var startDateTime = DateTime.Parse(startDate);
        var endDateTime = DateTime.Parse(endDate);

        using var db = new TaiDbContext();
        var endTime = end.Date.ToString("yyyy-MM-dd 23:59:59");
        var logsToDelete = await db.DailyLog
            .Where(log => log.Date >= startDateTime && log.Date <= endDateTime)
            .ToListAsync();

        db.DailyLog.RemoveRange(logsToDelete);

        var hoursToDelete = await db.HoursLog
            .Where(log => log.DataTime >= startDateTime && log.DataTime <= endDateTime)
            .ToListAsync();
        db.HoursLog.RemoveRange(hoursToDelete);
        await db.SaveChangesAsync();
    }

    public async Task ExportToExcelAsync(
        string dir,
        DateTime start,
        DateTime end,
        ExportOptions options)
    {
        start = new DateTime(start.Year, start.Month, 1, 0, 0, 0);
        end = new DateTime(end.Year, end.Month, DateTime.DaysInMonth(end.Year, end.Month), 23, 59, 59);

        using var db = new TaiDbContext();

        var days = await db.DailyLog
            .Where(m => m.Date >= start.Date && m.Date <= end.Date)
            .Include(m => m.AppModel)
            .ToListAsync();

        var hours = await db.HoursLog
            .Where(m => m.DataTime >= start && m.DataTime <= end)
            .Include(m => m.AppModel)
            .ToListAsync();

        using var workbook = new XLWorkbook();

        var worksheet1 = workbook.Worksheets.Add(options.Application.DailySheetName);
        var worksheet2 = workbook.Worksheets.Add(options.Application.TimePeriodSheetName);

        SetWorksheetHeaders(worksheet1, options.Application.DailyColumns);
        SetWorksheetHeaders(worksheet2, options.Application.TimePeriodColumns);

        FillDailyData(worksheet1, days, options);
        FillTimePeriodData(worksheet2, hours, options);

        var fileName = GenerateFileName(
            start,
            end,
            options.Application.StatisticsLabel,
            options.Culture,
            options.FileNamePrefix);
        var saveFilePath = Path.Combine(dir, $"{fileName}.xlsx");

        if (File.Exists(saveFilePath)) File.Delete(saveFilePath);
        workbook.SaveAs(saveFilePath);

        // 导出CSV
        await ExportCsvFiles(dir, fileName, days, hours, options);
    }

    public Task<int> GetDateRangeAppCountAsync(DateTime start, DateTime end)
    {
        IEnumerable<AppModel> apps = _appData.GetAllApps();
        using var db = new TaiDbContext();
        var res = db.DailyLog
            .Where(m => m.Date >= start && m.Date <= end && m.AppModelID != 0)
            .GroupBy(m => m.AppModelID)
            .CountAsync();
        return res;
    }

    public async Task<IEnumerable<HoursLogModel>> GetTimeRangelogListAsync(DateTime time)
    {
        time = new DateTime(time.Year, time.Month, time.Day, time.Hour, 0, 0);
        using var db = new TaiDbContext();
        var res = await db.HoursLog.Where(m => m.DataTime == time).ToListAsync();
        foreach (var log in res) log.AppModel = _appData.GetApp(log.AppModelID);
        return res;
    }

    public async Task<double[]> GetRangeTotalDataAsync(DateTime start, DateTime end)
    {
        var startDate = start.Date;
        var endDate = end.Date;

        using var db = new TaiDbContext();

        if (startDate == endDate)
        {
            // 处理单日24小时数据
            var dayStart = startDate;
            var dayEnd = dayStart.AddDays(1).AddTicks(-1);

            var hourlyData = await db.HoursLog
                .Where(log => log.DataTime >= dayStart && log.DataTime <= dayEnd)
                .GroupBy(log => new { log.DataTime.Year, log.DataTime.Month, log.DataTime.Day, log.DataTime.Hour })
                .Select(g => new
                {
                    g.Key.Hour,
                    Total = g.Sum(log => log.Time)
                })
                .ToDictionaryAsync(x => x.Hour, x => x.Total);

            var result = new double[24];
            for (var hour = 0; hour < 24; hour++)
                result[hour] = hourlyData.TryGetValue(hour, out var total) ? total : 0;
            return result;
        }
        else
        {
            // 处理多日数据
            var daysCount = (int)(endDate - startDate).TotalDays + 1;
            var dateRangeEnd = endDate.AddDays(1).AddTicks(-1);

            var dailyData = await db.DailyLog
                .Where(log => log.Date >= startDate && log.Date <= dateRangeEnd)
                .GroupBy(log => log.Date.Date)
                .Select(g => new
                {
                    Date = g.Key,
                    Total = g.Sum(log => log.Time)
                })
                .ToDictionaryAsync(x => x.Date, x => x.Total);

            var result = new double[daysCount];
            for (var i = 0; i < daysCount; i++)
            {
                var currentDate = startDate.AddDays(i);
                result[i] = dailyData.TryGetValue(currentDate, out var total) ? total : 0;
            }

            return result;
        }
    }

    public async Task<double[]> GetMonthTotalDataAsync(DateTime date)
    {
        using var db = new TaiDbContext();

        var dateArr = Time.GetYearDate(date);
        var data = await db.DailyLog
            .Where(log => log.Date >= dateArr[0].Date && log.Date <= dateArr[1].Date)
            .GroupBy(log => log.Date)
            .Select(g => new TimeDataModel
            {
                Total = g.Sum(log => log.Time),
                Time = g.Key
            })
            .ToArrayAsync();
        var result = new double[12];

        for (var i = 1; i < 13; i++)
        {
            var month = i < 10 ? "0" + i : i.ToString();
            var dayArr = Time.GetMonthDate(new DateTime(date.Year, i, 1));
            var total = data.Where(m => m.Time >= dayArr[0] && m.Time <= dayArr[1]).Sum(m => m.Total);
            result[i - 1] = total;
        }

        return result;
    }

    public async Task ClearAsync(int appID_)
    {
        using var db = new TaiDbContext();
        var dailyLogs = await db.DailyLog.Where(d => d.AppModelID == appID_).ToListAsync();
        db.DailyLog.RemoveRange(dailyLogs);

        var hoursLogs = await db.HoursLog.Where(h => h.AppModelID == appID_).ToListAsync();
        db.HoursLog.RemoveRange(hoursLogs);

        var appModel = db.App.FirstOrDefault(a => a.ID == appID_);
        if (appModel != null) appModel.TotalTime = 0;

        await db.SaveChangesAsync();
    }


    private string GenerateFileName(DateTime start, DateTime end, string statisticsLabel,
        CultureInfo culture, string prefix)
    {
        var dateFormat = culture.DateTimeFormat.YearMonthPattern;
        var dateRange = start.Year == end.Year && start.Month == end.Month
            ? start.ToString(dateFormat, culture)
            : $"{start.ToString(dateFormat, culture)}-{end.ToString(dateFormat, culture)}";

        return $"{prefix} {statisticsLabel} ({dateRange})";
    }

    private async Task ExportCsvFiles(string dir, string baseName, List<DailyLogModel> days, List<HoursLogModel> hours,
        ExportOptions options)
    {
        var csvOptions = new CsvConfiguration(options.Culture)
        {
            Delimiter = ",",
            Encoding = Encoding.UTF8
        };

        var dailyRecords = days.Select(x => new
        {
            Date = x.Date.ToString(options.Culture.DateTimeFormat.ShortDatePattern),
            AppName = x.AppModel?.Name,
            x.AppModel?.Description,
            Duration = x.Time,
            Category = x.AppModel?.Category?.Name ?? options.UncategorizedLabel
        });

        await WriteCsv(
            Path.Combine(dir, $"{baseName}-{options.Application.DailySheetName}.csv"),
            dailyRecords,
            csvOptions);

        // 导出时段数据
        var timeRecords = hours.Select(x => new
        {
            TimePeriod = x.DataTime.ToString("G", options.Culture),
            AppName = x.AppModel?.Name,
            x.AppModel?.Description,
            Duration = x.Time,
            Category = x.AppModel?.Category?.Name ?? options.UncategorizedLabel
        });

        await WriteCsv(
            Path.Combine(dir, $"{baseName}-{options.Application.TimePeriodSheetName}.csv"),
            timeRecords,
            csvOptions);
    }


    private void SetWorksheetHeaders(IXLWorksheet worksheet, string[] columns)
    {
        for (var i = 0; i < columns.Length; i++) worksheet.Cell(1, i + 1).Value = columns[i];
    }


    private void FillDailyData(IXLWorksheet worksheet, List<DailyLogModel> data, ExportOptions options)
    {
        for (var i = 0; i < data.Count; i++)
        {
            var item = data[i];
            worksheet.Cell(i + 2, 1).Value = item.Date.ToString(options.Culture.DateTimeFormat.ShortDatePattern);
            worksheet.Cell(i + 2, 2).Value = item.AppModel?.Name;
            worksheet.Cell(i + 2, 3).Value = item.AppModel?.Description;
            worksheet.Cell(i + 2, 4).Value = item.Time;
            worksheet.Cell(i + 2, 5).Value = item.AppModel?.Category?.Name ?? options.UncategorizedLabel;
        }
    }


    private void FillTimePeriodData(IXLWorksheet worksheet, List<HoursLogModel> data, ExportOptions options)
    {
        for (var i = 0; i < data.Count; i++)
        {
            var item = data[i];
            worksheet.Cell(i + 2, 1).Value = item.DataTime.ToString("G", options.Culture);
            worksheet.Cell(i + 2, 2).Value = item.AppModel?.Name;
            worksheet.Cell(i + 2, 3).Value = item.AppModel?.Description;
            worksheet.Cell(i + 2, 4).Value = item.Time;
            worksheet.Cell(i + 2, 5).Value = item.AppModel?.Category?.Name ?? options.UncategorizedLabel;
        }
    }


    private async Task WriteCsv<T>(string path, IEnumerable<T> records, CsvConfiguration config)
    {
        using var writer = new StreamWriter(path, false, config.Encoding);
        using var csv = new CsvWriter(writer, config);
        await csv.WriteRecordsAsync(records);
    }

    public struct CategoryHoursDataModel
    {
        public int Total { get; set; }
        public int CategoryID { get; set; }
        public DateTime Time { get; set; }
    }

    public struct ColumnItemDataModel
    {
        public int Total { get; set; }
        public int AppID { get; set; }
        public DateTime Time { get; set; }
    }

    public struct TimeDataModel
    {
        public int Total { get; set; }
        public DateTime Time { get; set; }
    }
}
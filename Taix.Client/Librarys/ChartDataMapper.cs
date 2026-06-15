using System;
using System.Collections.Generic;
using System.Linq;
using Taix.Client.Controls.Charts.Model;
using Taix.Client.Shared.Librarys;
using Taix.Client.Shared.Models;
using Taix.Client.Shared.Models.Db;

namespace Taix.Client.Librarys;

public static class ChartDataMapper
{
    public static List<ChartsDataModel> MapFromDailyLogs(IEnumerable<DailyLogModel> logs, AppModel? app = null, bool includeBadges = false, bool orderByValue = true)
    {
        var result = new List<ChartsDataModel>();
        foreach (var item in logs)
        {
            var appModel = item.AppModel ?? app;
            if (appModel == null) continue;
            var model = new ChartsDataModel
            {
                Data = item,
                Name = appModel.GetDisplayName(),
                Value = item.Time,
                Tag = Time.ToString(item.Time),
                PopupText = appModel.File,
                Icon = appModel.IconFile,
                DateTime = DateTime.SpecifyKind(item.Date, DateTimeKind.Utc).ToLocalTime()
            };

            if (includeBadges)
            {
                model.BadgeList = new List<ChartBadgeModel>();
                if (appModel.Category != null)
                    model.BadgeList.Add(new ChartBadgeModel
                    {
                        Name = appModel.Category.Name,
                        Color = appModel.Category.Color,
                        Type = ChartBadgeType.Category
                    });
            }

            result.Add(model);
        }
        return orderByValue ? result.OrderByDescending(x => x.Value).ToList() : result;
    }

    public static List<ChartsDataModel> MapFromWebSites(IEnumerable<WebSiteModel> sites, bool includeBadges = false)
    {
        var result = new List<ChartsDataModel>();
        foreach (var item in sites)
        {
            var model = new ChartsDataModel
            {
                Data = item,
                Name = !string.IsNullOrEmpty(item.Alias) ? item.Alias : item.Title,
                Value = item.Duration,
                Tag = Time.ToString(item.Duration),
                PopupText = item.Domain,
                Icon = item.IconFile
            };

            if (includeBadges)
            {
                model.BadgeList = new List<ChartBadgeModel>();
                if (item.Category != null)
                    model.BadgeList.Add(new ChartBadgeModel
                    {
                        Name = item.Category.Name,
                        Color = item.Category.Color,
                        Type = ChartBadgeType.Category
                    });
            }

            result.Add(model);
        }
        return result.OrderByDescending(x => x.Value).ToList();
    }

    public static List<ChartsDataModel> MapFromHoursLogs(IEnumerable<HoursLogModel> logs, bool includeBadges = false)
    {
        var result = new List<ChartsDataModel>();
        foreach (var item in logs)
        {
            if (item.AppModel == null) continue;
            var model = new ChartsDataModel
            {
                Data = item,
                Name = item.AppModel.GetDisplayName(),
                Value = item.Time,
                Tag = Time.ToString(item.Time),
                PopupText = item.AppModel.File,
                Icon = item.AppModel.IconFile,
                DateTime = DateTime.SpecifyKind(item.DataTime, DateTimeKind.Utc).ToLocalTime()
            };

            if (includeBadges)
            {
                model.BadgeList = new List<ChartBadgeModel>();
                if (item.AppModel.Category != null)
                    model.BadgeList.Add(new ChartBadgeModel
                    {
                        Name = item.AppModel.Category.Name,
                        Color = item.AppModel.Category.Color,
                        Type = ChartBadgeType.Category
                    });
            }

            result.Add(model);
        }
        return result.OrderByDescending(x => x.Value).ToList();
    }
}

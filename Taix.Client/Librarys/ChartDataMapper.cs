using System.Collections.Generic;
using System.Linq;
using Taix.Client.Controls.Charts.Model;
using Taix.Client.Shared.Librarys;
using Taix.Client.Shared.Models;
using Taix.Client.Shared.Models.Db;

namespace Taix.Client.Librarys;

public static class ChartDataMapper
{
    public static List<ChartsDataModel> MapFromDailyLogs(IEnumerable<DailyLogModel> logs, bool includeBadges = false, IReadOnlyCollection<string>? ignoreList = null, bool orderByValue = true)
    {
        var result = new List<ChartsDataModel>();
        foreach (var item in logs)
        {
            if (item.AppModel == null) continue;
            var model = new ChartsDataModel
            {
                Data = item,
                Name = !string.IsNullOrEmpty(item.AppModel.Alias) ? item.AppModel.Alias
                    : string.IsNullOrEmpty(item.AppModel.Description) ? item.AppModel.Name : item.AppModel.Description,
                Value = item.Time,
                Tag = Time.ToString(item.Time),
                PopupText = item.AppModel.File,
                Icon = item.AppModel.IconFile,
                DateTime = item.Date
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
                if (ignoreList != null && ignoreList.Contains(item.AppModel.Name))
                    model.BadgeList.Add(ChartBadgeModel.IgnoreBadge);
            }

            result.Add(model);
        }
        return orderByValue ? result.OrderByDescending(x => x.Value).ToList() : result;
    }

    public static List<ChartsDataModel> MapFromWebSites(IEnumerable<WebSiteModel> sites, bool includeBadges = false, IReadOnlyCollection<string>? ignoreList = null)
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
                if (ignoreList != null && ignoreList.Contains(item.Domain))
                    model.BadgeList.Add(ChartBadgeModel.IgnoreBadge);
            }

            result.Add(model);
        }
        return result.OrderByDescending(x => x.Value).ToList();
    }

    public static List<ChartsDataModel> MapFromHoursLogs(IEnumerable<HoursLogModel> logs, bool includeBadges = false, IReadOnlyCollection<string>? ignoreList = null)
    {
        var result = new List<ChartsDataModel>();
        foreach (var item in logs)
        {
            if (item.AppModel == null) continue;
            var model = new ChartsDataModel
            {
                Data = item,
                Name = !string.IsNullOrEmpty(item.AppModel.Alias) ? item.AppModel.Alias
                    : string.IsNullOrEmpty(item.AppModel.Description) ? item.AppModel.Name : item.AppModel.Description,
                Value = item.Time,
                Tag = Time.ToString(item.Time),
                PopupText = item.AppModel.File,
                Icon = item.AppModel.IconFile
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
                if (ignoreList != null && ignoreList.Contains(item.AppModel.Name))
                    model.BadgeList.Add(ChartBadgeModel.IgnoreBadge);
            }

            result.Add(model);
        }
        return result.OrderByDescending(x => x.Value).ToList();
    }
}

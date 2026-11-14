using System.Collections.Generic;
using Taix.Client.Shared.Models;
using Taix.Client.Shared.Models.Web;

namespace Taix.Client.Shared.Models.Data;

public class OverviewBundleModel
{
    public List<DailyLogModel> Apps { get; set; } = [];
    public List<WebSiteModel> Sites { get; set; } = [];
    public List<ColumnDataModel> AppCategoryHours { get; set; } = [];
    public List<ColumnDataModel> WebCategoryHours { get; set; } = [];
}

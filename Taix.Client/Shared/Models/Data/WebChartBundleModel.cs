using System.Collections.Generic;
using Taix.Client.Shared.Models.Web;

namespace Taix.Client.Shared.Models.Data;

public class WebChartBundleModel
{
    /// <summary>完整站点分类表（含无数据分类），渲染颜色/图标直接取用，免单独请求</summary>
    public List<WebSiteCategoryModel> Categories { get; set; } = [];
    public List<InfrastructureDataModel> CategoryStatistics { get; set; } = [];
    public List<ColumnDataModel> CategoryColumns { get; set; } = [];
    public List<WebSiteModel> TopSites { get; set; } = [];
    public int DurationTotal { get; set; }
    public int SitesTotal { get; set; }
    public int PagesTotal { get; set; }
}

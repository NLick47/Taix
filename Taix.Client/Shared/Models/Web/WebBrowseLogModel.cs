using System;

namespace Taix.Client.Shared.Models.Db;

/// <summary>
/// 网页浏览记录
/// </summary>
public class WebBrowseLogModel
{
    public int ID { get; set; }

    public int UrlId { get; set; }

    public WebUrlModel Url { get; set; }

    /// <summary>
    /// 统计时段（YYYY-MM-dd HH:00:00)
    /// </summary>
    public DateTime LogTime { get; set; }

    /// <summary>
    /// 使用时长（单位：秒）
    /// </summary>
    public int Duration { get; set; }

    /// <summary>
    /// 网站ID
    /// </summary>
    public int SiteId { get; set; }

    public WebSiteModel Site { get; set; }
}

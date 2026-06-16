using System;
using Taix.Client.Shared.Models;
using Taix.Client.Shared.Models.Web;

namespace Taix.Client.Models.Navigation;

public class DetailNavigationContext
{
    public required AppModel App { get; init; }
    public int PeriodType { get; init; }
    public DateTime Date { get; init; }

    public static DetailNavigationContext Create(AppModel app, int tabIndex, DateTime date)
    {
        return new DetailNavigationContext
        {
            App = app,
            PeriodType = tabIndex,
            Date = date
        };
    }
}

public class WebSiteDetailNavigationContext
{
    public required WebSiteModel WebSite { get; init; }
    public int PeriodType { get; init; }
    public DateTime Date { get; init; }

    public static WebSiteDetailNavigationContext Create(WebSiteModel webSite, int tabIndex, DateTime date)
    {
        return new WebSiteDetailNavigationContext
        {
            WebSite = webSite,
            PeriodType = tabIndex,
            Date = date
        };
    }
}

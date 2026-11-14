using System.Collections.Generic;
using Taix.Client.Shared.Models;

namespace Taix.Client.Shared.Models.Data;

public class ChartBundleModel
{
    public List<CategoryColumnModel> Columns { get; set; } = [];
    public List<DailyLogModel> Top { get; set; } = [];
    public int AppCount { get; set; }
    public int AppCountPrev { get; set; }
    public double TotalSecondsPrev { get; set; }
    public WebTotalsModel WebPrev { get; set; } = new();
}

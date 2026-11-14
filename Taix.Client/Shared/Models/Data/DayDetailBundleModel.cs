using System.Collections.Generic;
using Taix.Client.Shared.Models;

namespace Taix.Client.Shared.Models.Data;

public class DayDetailBundleModel
{
    public List<DailyLogModel> Apps { get; set; } = [];
    public List<AppSessionModel> Sessions { get; set; } = [];
}

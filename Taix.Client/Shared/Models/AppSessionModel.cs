using System;

namespace Taix.Client.Shared.Models;

public class AppSessionModel
{
    public int ID { get; set; }

    public int AppModelID { get; set; }

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    public int Duration { get; set; }

    public AppModel AppModel { get; set; }
}

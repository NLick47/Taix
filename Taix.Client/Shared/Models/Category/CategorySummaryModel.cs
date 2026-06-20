using System;
using System.Collections.Generic;

namespace Taix.Client.Shared.Models.Category;

public record CategorySummaryModel
{
    public string CategoryName { get; init; } = string.Empty;

    public long TotalSeconds { get; init; }

    /// <summary>上一等长周期（昨日/上周/上月/去年）的总时长，用于环比；无对比时为 0</summary>
    public long PreviousTotalSeconds { get; init; }

    public int ActiveDays { get; init; }

    public long AverageDailySeconds { get; init; }

    public List<DailyPointModel> DailyTrend { get; init; } = new();

    /// <summary>按本地时区小时的时长分布，固定 24 个元素</summary>
    public List<long> HourlyDistribution { get; init; } = new();
}

public record DailyPointModel
{
    public DateTime Date { get; init; }

    public long Seconds { get; init; }
}

public record CategoryMemberModel
{
    public string Name { get; init; } = string.Empty;

    public string? IconFile { get; init; }

    public long Seconds { get; init; }
}

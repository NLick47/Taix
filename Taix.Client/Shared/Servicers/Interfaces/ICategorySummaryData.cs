using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Taix.Client.Shared.Models.Category;

namespace Taix.Client.Shared.Servicers.Interfaces;

public interface ICategorySummaryData
{
    Task<CategorySummaryModel> GetSummaryAsync(
        CategorySummaryKind kind,
        int categoryId,
        DateTime start,
        DateTime end,
        DateTime? prevStart = null,
        DateTime? prevEnd = null,
        CancellationToken cancellationToken = default);

    /// <summary>取指定区间内该分类的成员（应用/网站）按时长聚合的 Top-N，用于柱状图点击列展开</summary>
    Task<List<CategoryMemberModel>> GetMembersAsync(
        CategorySummaryKind kind,
        int categoryId,
        DateTime start,
        DateTime end,
        CancellationToken cancellationToken = default);
}

public enum CategorySummaryKind
{
    App = 0,
    Web = 1,
}

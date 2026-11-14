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
        bool withMembers = false,
        bool withModels = false,
        CancellationToken cancellationToken = default);

    Task<List<CategoryMemberModel>> GetMembersAsync(
        CategorySummaryKind kind,
        int categoryId,
        DateTime start,
        DateTime end,
        bool withModels = false,
        CancellationToken cancellationToken = default);
}

public enum CategorySummaryKind
{
    App = 0,
    Web = 1,
}

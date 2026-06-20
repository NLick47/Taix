using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Taix.Client.Librarys.Api;
using Taix.Client.Shared.Models.Category;
using Taix.Client.Shared.Servicers.Interfaces;

namespace Taix.Client.Servicers.Instances;

public class ApiCategorySummaryData : ICategorySummaryData
{
    private readonly ITaixApiClient _apiClient;

    public ApiCategorySummaryData(ITaixApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public Task<CategorySummaryModel> GetSummaryAsync(
        CategorySummaryKind kind,
        int categoryId,
        DateTime start,
        DateTime end,
        DateTime? prevStart = null,
        DateTime? prevEnd = null,
        CancellationToken cancellationToken = default)
    {
        return kind switch
        {
            CategorySummaryKind.App => _apiClient.GetAppCategorySummaryAsync(categoryId, start, end, prevStart, prevEnd, cancellationToken),
            CategorySummaryKind.Web => _apiClient.GetWebCategorySummaryAsync(categoryId, start, end, prevStart, prevEnd, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
    }

    public Task<List<CategoryMemberModel>> GetMembersAsync(
        CategorySummaryKind kind,
        int categoryId,
        DateTime start,
        DateTime end,
        CancellationToken cancellationToken = default)
    {
        return kind switch
        {
            CategorySummaryKind.App => _apiClient.GetAppCategoryMembersAsync(categoryId, start, end, cancellationToken),
            CategorySummaryKind.Web => _apiClient.GetWebCategoryMembersAsync(categoryId, start, end, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
    }
}

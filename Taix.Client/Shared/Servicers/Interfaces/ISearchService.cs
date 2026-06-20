using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Taix.Client.Shared.Models.Search;

namespace Taix.Client.Shared.Servicers.Interfaces;

public interface ISearchService
{
    event Action? SearchToggleRequested;

    Task ShowAsync();

    Task<IReadOnlyList<SearchResultItem>> SearchAsync(string keyword, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SearchResultItem>> GetTodayHighlightsAsync(int max = 8, CancellationToken cancellationToken = default);

    void NavigateTo(SearchResultItem item);

    IReadOnlyList<SearchResultItem> GetCategoryCardItems(SearchResultItem cardItem);

    void InvalidateCache();
}

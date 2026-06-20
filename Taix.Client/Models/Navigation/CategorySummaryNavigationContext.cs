using Taix.Client.Shared.Servicers.Interfaces;

namespace Taix.Client.Models.Navigation;

public class CategorySummaryNavigationContext
{
    public required CategorySummaryKind Kind { get; init; }
    public required int CategoryId { get; init; }
    public string? CategoryName { get; init; }

    public static CategorySummaryNavigationContext Create(CategorySummaryKind kind, int categoryId, string? name = null)
    {
        return new CategorySummaryNavigationContext
        {
            Kind = kind,
            CategoryId = categoryId,
            CategoryName = name,
        };
    }
}

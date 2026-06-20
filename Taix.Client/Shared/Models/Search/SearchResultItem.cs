using System;
using System.Collections.Generic;

namespace Taix.Client.Shared.Models.Search;

public enum SearchResultType
{
    App,
    WebSite,
    CategoryApp,
    CategoryWeb,
}

// 行的形态（与 Type 正交）：Item / Header / CategoryCard
public enum SearchRowKind
{
    Item = 0,
    Header,
    CategoryCard,
}

// 分类卡片中的单个药丸（名称 + 时长）
public sealed record CategoryPill(string Name, string? UsageText);

public record SearchResultItem(
    SearchResultType Type,
    int Id,
    string DisplayName,
    string SecondaryText,
    string? IconFile,
    object? PayloadModel,
    string? UsageText = null,
    SearchRowKind Kind = SearchRowKind.Item,
    string? Color = null,
    IReadOnlyList<CategoryPill>? CategoryPills = null,
    int OverflowCount = 0,
    object? CategoryPayloads = null)
{
    public bool IsHeader => Kind == SearchRowKind.Header;
    public bool IsCategoryCard => Kind == SearchRowKind.CategoryCard;
    public bool IsItemRow => Kind == SearchRowKind.Item;
    public bool HasOverflow => OverflowCount > 0;
    public string OverflowText => OverflowCount > 0 ? $"+{OverflowCount}" : string.Empty;
}

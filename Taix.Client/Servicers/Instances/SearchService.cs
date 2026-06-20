using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Taix.Client.Logging;
using Taix.Client.Models.Navigation;
using Taix.Client.Servicers.Interfaces;
using Taix.Client.Shared.Librarys;
using Taix.Client.Shared.Models;
using Taix.Client.Shared.Models.Data;
using Taix.Client.Shared.Models.Search;
using Taix.Client.Shared.Models.Web;
using Taix.Client.Shared.Servicers.Interfaces;
using Taix.Client.Views;

namespace Taix.Client.Servicers.Instances;

public class SearchService : ISearchService
{
    // 别名硬编码映射，参考 CategoryAppListPageViewModel
    private static readonly Dictionary<string, string> _aliasHints = new(StringComparer.OrdinalIgnoreCase)
    {
        ["vscode"] = "Visual Studio Code",
        ["ps"] = "Photoshop",
    };

    private readonly IAppData _appData;
    private readonly IWebSiteData _webSiteData;
    private readonly IData _dataService;
    private readonly IWebData _webData;
    private readonly ICategorys _categorys;
    private readonly INavigationService _navigationService;

    private IReadOnlyCollection<AppModel>? _cachedApps;
    private IReadOnlyCollection<WebSiteModel>? _cachedSites;
    private IReadOnlyCollection<CategoryModel>? _cachedAppCategories;
    private IReadOnlyCollection<WebSiteCategoryModel>? _cachedWebCategories;
    private DateTime _cacheTime;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);
    private readonly SemaphoreSlim _cacheLock = new(1, 1);

    public event Action? SearchToggleRequested;

    public SearchService(
        IAppData appData,
        IWebSiteData webSiteData,
        IData dataService,
        IWebData webData,
        ICategorys categorys,
        INavigationService navigationService)
    {
        _appData = appData;
        _webSiteData = webSiteData;
        _dataService = dataService;
        _webData = webData;
        _categorys = categorys;
        _navigationService = navigationService;
    }

    public Task ShowAsync()
    {
        // Toggle：不再创建独立窗口，交由搜索宿主（MainViewModel）切换覆盖层布尔状态
        SearchToggleRequested?.Invoke();
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<SearchResultItem>> SearchAsync(string keyword, CancellationToken cancellationToken = default)
    {
        keyword = keyword?.Trim() ?? string.Empty;
        if (keyword.Length == 0) return Array.Empty<SearchResultItem>();

        var (apps, sites, appCategories, webCategories) = await GetCorpusAsync(cancellationToken).ConfigureAwait(false);

        // 别名匹配
        string? aliased = null;
        if (_aliasHints.TryGetValue(keyword, out var mapped) &&
            !mapped.Equals(keyword, StringComparison.OrdinalIgnoreCase))
        {
            aliased = mapped;
        }

        var results = new List<(int score, SearchResultItem item)>();

        // 分类加权，确保分类排在同名项前面
        const int CategoryBoost = 5;

        foreach (var cat in appCategories)
        {
            var score = ScoreOne(cat.Name ?? string.Empty, keyword);
            if (aliased != null)
            {
                var alt = ScoreOne(cat.Name ?? string.Empty, aliased);
                if (alt > 0) score = Math.Max(score, alt - 5);
            }
            if (score > 0)
            {
                results.Add((score + CategoryBoost, new SearchResultItem(
                    Type: SearchResultType.CategoryApp,
                    Id: cat.ID,
                    DisplayName: cat.Name ?? string.Empty,
                    SecondaryText: ResolveResourceString("SearchTypeCategoryApp", "应用分类"),
                    IconFile: cat.IconFile,
                    PayloadModel: cat)));
            }
        }

        foreach (var cat in webCategories)
        {
            var score = ScoreOne(cat.Name ?? string.Empty, keyword);
            if (aliased != null)
            {
                var alt = ScoreOne(cat.Name ?? string.Empty, aliased);
                if (alt > 0) score = Math.Max(score, alt - 5);
            }
            if (score > 0)
            {
                results.Add((score + CategoryBoost, new SearchResultItem(
                    Type: SearchResultType.CategoryWeb,
                    Id: cat.ID,
                    DisplayName: cat.Name ?? string.Empty,
                    SecondaryText: ResolveResourceString("SearchTypeCategoryWeb", "网站分类"),
                    IconFile: cat.IconFile,
                    PayloadModel: cat)));
            }
        }

        foreach (var app in apps)
        {
            var score = ScoreApp(app, keyword, aliased);
            if (score > 0)
            {
                results.Add((score, new SearchResultItem(
                    Type: SearchResultType.App,
                    Id: app.ID,
                    DisplayName: app.GetDisplayName(),
                    SecondaryText: app.Name ?? string.Empty,
                    IconFile: app.IconFile,
                    PayloadModel: app)));
            }
        }

        foreach (var site in sites)
        {
            var score = ScoreSite(site, keyword, aliased);
            if (score > 0)
            {
                results.Add((score, new SearchResultItem(
                    Type: SearchResultType.WebSite,
                    Id: site.ID,
                    DisplayName: site.GetDisplayName(),
                    SecondaryText: site.Domain ?? string.Empty,
                    IconFile: site.IconFile,
                    PayloadModel: site)));
            }
        }

        return results
            .OrderByDescending(x => x.score)
            .ThenBy(x => x.item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.item)
            .Take(50)
            .ToList();
    }

    private static string ResolveResourceString(string key, string fallback)
    {
        if (Avalonia.Application.Current?.Resources.TryGetResource(key, null, out var res) == true && res is string s)
            return s;
        return fallback;
    }

    public async Task<IReadOnlyList<SearchResultItem>> GetTodayHighlightsAsync(int max = 8, CancellationToken cancellationToken = default)
    {
        var today = DateTime.Now.Date;
        var halfMax = Math.Max(1, max / 2);

        var appsTask = SafeAsync(
            () => _dataService.GetDateRangelogListAsync(today, today, take: halfMax, cancellationToken: cancellationToken),
            Enumerable.Empty<DailyLogModel>(),
            "今日应用列表");

        var sitesTask = SafeAsync(
            () => _webData.GetDateRangeWebSiteListAsync(today, today, take: halfMax, cancellationToken: cancellationToken),
            (IReadOnlyList<WebSiteModel>)Array.Empty<WebSiteModel>(),
            "今日网站列表");

        // 分类列表从 corpus 缓存获取
        var corpusTask = GetCorpusAsync(cancellationToken);

        // 今日各分类总时长，用于卡片展示和排序
        var appCategoryTodayTask = SafeAsync<IReadOnlyList<ColumnDataModel>>(
            () => _dataService.GetCategoryHoursDataAsync(today, cancellationToken),
            Array.Empty<ColumnDataModel>(),
            "今日应用分类时长");
        var webCategoryTodayTask = SafeAsync<IReadOnlyList<ColumnDataModel>>(
            () => _webData.GetBrowseDataByCategoryStatisticsAsync(today, today, cancellationToken),
            Array.Empty<ColumnDataModel>(),
            "今日网站分类时长");

        await Task.WhenAll(appsTask, sitesTask, corpusTask, appCategoryTodayTask, webCategoryTodayTask).ConfigureAwait(false);

        var (_, _, appCategories, webCategories) = await corpusTask;
        var appCategoryToday = await appCategoryTodayTask;
        var webCategoryToday = await webCategoryTodayTask;

        // ColumnDataModel.Values 聚合为 categoryId -> totalSeconds
        var appCatSeconds = SumByCategory(appCategoryToday);
        var webCatSeconds = SumByCategory(webCategoryToday);

        var result = new List<SearchResultItem>(max + 16);

        // 分类区段
        var categoryCount = appCategories.Count + webCategories.Count;
        if (categoryCount > 0)
        {
            result.Add(MakeHeader(ResolveResourceString("SearchSectionCategories", "浏览分类")));

            if (appCategories.Count > 0)
            {
                // 按今日时长降序，无数据放尾部并按名称排序
                var sortedAppCats = appCategories
                    .OrderByDescending(c => appCatSeconds.GetValueOrDefault(c.ID, 0))
                    .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                result.Add(BuildCategoryCard(
                    type: SearchResultType.CategoryApp,
                    title: ResolveResourceString("SearchTypeApp", "应用"),
                    sorted: sortedAppCats,
                    nameOf: c => c.Name ?? string.Empty,
                    iconOf: c => c.IconFile,
                    secondsOf: c => appCatSeconds.GetValueOrDefault(c.ID, 0)));
            }

            if (webCategories.Count > 0)
            {
                var sortedWebCats = webCategories
                    .OrderByDescending(c => webCatSeconds.GetValueOrDefault(c.ID, 0))
                    .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                result.Add(BuildCategoryCard(
                    type: SearchResultType.CategoryWeb,
                    title: ResolveResourceString("SearchTypeWeb", "网站"),
                    sorted: sortedWebCats,
                    nameOf: c => c.Name ?? string.Empty,
                    iconOf: c => c.IconFile,
                    secondsOf: c => webCatSeconds.GetValueOrDefault(c.ID, 0)));
            }
        }

        // 今日活跃区段
        var todayApps = await appsTask;
        var todaySites = await sitesTask;
        var todayHasItems = todayApps.Any(l => l.AppModel != null) || todaySites.Any();

        if (todayHasItems)
        {
            result.Add(MakeHeader(ResolveResourceString("SearchSectionToday", "今日活跃")));

            int activeCount = 0;
            foreach (var log in todayApps)
            {
                if (log.AppModel == null) continue;
                result.Add(new SearchResultItem(
                    Type: SearchResultType.App,
                    Id: log.AppModel.ID,
                    DisplayName: log.AppModel.GetDisplayName(),
                    SecondaryText: log.AppModel.Name ?? string.Empty,
                    IconFile: log.AppModel.IconFile,
                    PayloadModel: log.AppModel,
                    UsageText: Time.ToString(log.Time)));
                activeCount++;
                if (activeCount >= halfMax) break;
            }

            foreach (var site in todaySites)
            {
                result.Add(new SearchResultItem(
                    Type: SearchResultType.WebSite,
                    Id: site.ID,
                    DisplayName: site.GetDisplayName(),
                    SecondaryText: site.Domain ?? string.Empty,
                    IconFile: site.IconFile,
                    PayloadModel: site,
                    UsageText: Time.ToString(site.Duration)));
                activeCount++;
                if (activeCount >= max) break;
            }
        }

        return result;
    }

    private static Dictionary<int, long> SumByCategory(IReadOnlyList<ColumnDataModel> data)
    {
        var dict = new Dictionary<int, long>(data.Count);
        foreach (var row in data)
        {
            long sum = 0;
            foreach (var v in row.Values) sum += (long)v;
            dict[row.CategoryID] = sum;
        }
        return dict;
    }

    private const int CategoryCardPillTop = 3;
    private SearchResultItem BuildCategoryCard<TCat>(
        SearchResultType type,
        string title,
        IReadOnlyList<TCat> sorted,
        Func<TCat, string> nameOf,
        Func<TCat, string?> iconOf,
        Func<TCat, long> secondsOf)
    {
        var pills = new List<CategoryPill>(Math.Min(CategoryCardPillTop, sorted.Count));
        var children = new List<SearchResultItem>(sorted.Count);
        long totalSeconds = 0;

        for (var i = 0; i < sorted.Count; i++)
        {
            var cat = sorted[i];
            var name = nameOf(cat);
            var icon = iconOf(cat);
            var secs = secondsOf(cat);
            totalSeconds += secs;
            var usageText = secs > 0 ? Time.ToString((int)secs) : null;

            if (i < CategoryCardPillTop)
            {
                pills.Add(new CategoryPill(name, usageText));
            }

            // 展开子项复用普通 Item 行
            children.Add(new SearchResultItem(
                Type: type,
                Id: GetCategoryId(cat),
                DisplayName: name,
                SecondaryText: string.Empty,
                IconFile: icon,
                PayloadModel: cat,
                UsageText: usageText));
        }

        var overflow = Math.Max(0, sorted.Count - CategoryCardPillTop);
        return new SearchResultItem(
            Type: type,
            Id: -1,
            DisplayName: title,
            SecondaryText: string.Empty,
            IconFile: null,
            PayloadModel: null,
            UsageText: totalSeconds > 0 ? Time.ToString((int)totalSeconds) : null,
            Kind: SearchRowKind.CategoryCard,
            CategoryPills: pills,
            OverflowCount: overflow,
            CategoryPayloads: children);
    }

    private static int GetCategoryId(object? cat) => cat switch
    {
        CategoryModel a => a.ID,
        WebSiteCategoryModel w => w.ID,
        _ => 0,
    };

    private static SearchResultItem MakeHeader(string title)
        => new(
            Type: SearchResultType.App,
            Id: -1,
            DisplayName: title,
            SecondaryText: string.Empty,
            IconFile: null,
            PayloadModel: null,
            Kind: SearchRowKind.Header);

    public void NavigateTo(SearchResultItem item)
    {
        switch (item.Type)
        {
            case SearchResultType.App when item.PayloadModel is AppModel app:
                _navigationService.NavigateTo(
                    nameof(DetailPage),
                    DetailNavigationContext.Create(app, 0, DateTime.Today));
                break;
            case SearchResultType.WebSite when item.PayloadModel is WebSiteModel site:
                _navigationService.NavigateTo(
                    nameof(WebSiteDetailPage),
                    WebSiteDetailNavigationContext.Create(site, 0, DateTime.Today));
                break;
            case SearchResultType.CategoryApp when item.PayloadModel is CategoryModel appCat:
                _navigationService.NavigateTo(
                    nameof(CategorySummaryPage),
                    CategorySummaryNavigationContext.Create(CategorySummaryKind.App, appCat.ID, appCat.Name));
                break;
            case SearchResultType.CategoryWeb when item.PayloadModel is WebSiteCategoryModel webCat:
                _navigationService.NavigateTo(
                    nameof(CategorySummaryPage),
                    CategorySummaryNavigationContext.Create(CategorySummaryKind.Web, webCat.ID, webCat.Name));
                break;
        }
    }

    public IReadOnlyList<SearchResultItem> GetCategoryCardItems(SearchResultItem cardItem)
    {
        if (!cardItem.IsCategoryCard) return Array.Empty<SearchResultItem>();
        return cardItem.CategoryPayloads as IReadOnlyList<SearchResultItem>
            ?? Array.Empty<SearchResultItem>();
    }

    public void InvalidateCache()
    {
        _cachedApps = null;
        _cachedSites = null;
        _cachedAppCategories = null;
        _cachedWebCategories = null;
        _cacheTime = default;
    }

    private async Task<(
        IReadOnlyCollection<AppModel> apps,
        IReadOnlyCollection<WebSiteModel> sites,
        IReadOnlyCollection<CategoryModel> appCategories,
        IReadOnlyCollection<WebSiteCategoryModel> webCategories)>
        GetCorpusAsync(CancellationToken cancellationToken)
    {
        await _cacheLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_cachedApps == null || _cachedSites == null ||
                _cachedAppCategories == null || _cachedWebCategories == null ||
                DateTime.UtcNow - _cacheTime > CacheTtl)
            {
                _cachedApps = await SafeAsync(
                    () => _appData.GetAllAppsAsync(cancellationToken),
                    (IReadOnlyCollection<AppModel>)Array.Empty<AppModel>(),
                    "应用全集").ConfigureAwait(false);

                _cachedSites = await SafeAsync(
                    () => _webSiteData.GetAllWebSitesAsync(cancellationToken),
                    (IReadOnlyCollection<WebSiteModel>)Array.Empty<WebSiteModel>(),
                    "网站全集").ConfigureAwait(false);

                _cachedAppCategories = await SafeAsync<IReadOnlyCollection<CategoryModel>>(
                    async () => await _categorys.GetCategoriesAsync(cancellationToken),
                    Array.Empty<CategoryModel>(),
                    "应用分类列表").ConfigureAwait(false);

                _cachedWebCategories = await SafeAsync<IReadOnlyCollection<WebSiteCategoryModel>>(
                    async () => await _webData.GetWebSiteCategoriesAsync(cancellationToken),
                    Array.Empty<WebSiteCategoryModel>(),
                    "网站分类列表").ConfigureAwait(false);

                _cacheTime = DateTime.UtcNow;
            }
            return (_cachedApps!, _cachedSites!, _cachedAppCategories!, _cachedWebCategories!);
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    private static async Task<T> SafeAsync<T>(Func<Task<T>> getter, T fallback, string label)
    {
        try
        {
            return await getter().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.Warn($"加载{label}失败: {ex.Message}");
            return fallback;
        }
    }

    private static int ScoreApp(AppModel app, string keyword, string? aliased)
    {
        var fields = new[] { app.Alias, app.Description, app.Name };
        return ScoreFields(fields, keyword, aliased);
    }

    private static int ScoreSite(WebSiteModel site, string keyword, string? aliased)
    {
        var fields = new[] { site.Alias, site.Title, site.Domain };
        return ScoreFields(fields, keyword, aliased);
    }

    private static int ScoreFields(IReadOnlyList<string?> fields, string keyword, string? aliased)
    {
        int best = 0;
        foreach (var f in fields)
        {
            if (string.IsNullOrEmpty(f)) continue;
            int score = ScoreOne(f!, keyword);
            if (aliased != null)
            {
                var alt = ScoreOne(f!, aliased);
                if (alt > 0) score = Math.Max(score, alt - 5);
            }
            if (score > best) best = score;
        }
        return best;
    }

    // 完全匹配 100，前缀 50，子串 30；通配符/正则走 WildcardHelper（20）
    private static int ScoreOne(string field, string keyword)
    {
        if (field.Equals(keyword, StringComparison.OrdinalIgnoreCase)) return 100;
        if (field.StartsWith(keyword, StringComparison.OrdinalIgnoreCase)) return 50;
        if (field.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0) return 30;
        if (HasWildcardOrRegex(keyword) && WildcardHelper.IsMatch(field, keyword)) return 20;
        return 0;
    }

    private static bool HasWildcardOrRegex(string keyword)
    {
        foreach (var c in keyword)
        {
            if (c is '*' or '?' or '^' or '$' or '[' or ']' or '(' or ')'
                or '{' or '}' or '|' or '+' or '\\') return true;
        }
        return false;
    }
}

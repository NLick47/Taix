using System;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ReactiveUI;
using ReactiveUI.Avalonia;
using Taix.Client.Models;
using Taix.Client.Shared.Models.Search;
using Taix.Client.Shared.Servicers.Interfaces;

namespace Taix.Client.ViewModels;

public class SearchPaletteViewModel : ModelBase
{
    private readonly ISearchService _searchService;
    private readonly CompositeDisposable _disposables = new();
    private string _keyword = string.Empty;
    private SearchResultItem? _selectedItem;
    private bool _isEmpty;
    private bool _isShowingDefault = true;
    private string _statusText = string.Empty;
    private string _resultsCountText = string.Empty;

    public SearchPaletteViewModel(ISearchService searchService)
    {
        _searchService = searchService;
        Results = new ObservableCollection<SearchResultItem>();

        CloseCommand = ReactiveCommand.Create(() => CloseRequested?.Invoke());
        ClearKeywordCommand = ReactiveCommand.Create(() => { Keyword = string.Empty; });

        // 节流 150ms 触发搜索
        this.WhenAnyValue(x => x.Keyword)
            .Throttle(TimeSpan.FromMilliseconds(150))
            .ObserveOn(AvaloniaScheduler.Instance)
            .Select(kw => Observable.FromAsync(_ => OnKeywordChangedAsync(kw)))
            .Switch()
            .Subscribe()
            .DisposeWith(_disposables);

        _ = LoadDefaultAsync();
    }

    public ReactiveCommand<Unit, Unit> CloseCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearKeywordCommand { get; }

    // 已展开的分类卡片 → (展开起始位置, 子项数量)
    private readonly System.Collections.Generic.Dictionary<SearchResultItem, (int startIndex, int childCount)> _expanded = new();

    public string Keyword
    {
        get => _keyword;
        set
        {
            _keyword = value;
            OnPropertyChanged();
        }
    }

    public SearchResultItem? SelectedItem
    {
        get => _selectedItem;
        set
        {
            _selectedItem = value;
            OnPropertyChanged();
        }
    }

    public bool IsEmpty
    {
        get => _isEmpty;
        set
        {
            _isEmpty = value;
            OnPropertyChanged();
        }
    }

    public bool IsShowingDefault
    {
        get => _isShowingDefault;
        set
        {
            _isShowingDefault = value;
            OnPropertyChanged();
        }
    }

    public string StatusText
    {
        get => _statusText;
        set
        {
            _statusText = value;
            OnPropertyChanged();
        }
    }

    public string ResultsCountText
    {
        get => _resultsCountText;
        set
        {
            _resultsCountText = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<SearchResultItem> Results { get; }

    public Action? CloseRequested { get; set; }

    public void TriggerSelected()
    {
        if (SelectedItem == null || SelectedItem.IsHeader) return;

        // 分类卡片 Enter 直接跳到该卡片下今日最活跃的分类（已按时长降序）；多选时用 → 展开
        if (SelectedItem.IsCategoryCard)
        {
            var children = _searchService.GetCategoryCardItems(SelectedItem);
            if (children.Count == 0) return;
            _searchService.NavigateTo(children[0]);
            CloseRequested?.Invoke();
            return;
        }

        _searchService.NavigateTo(SelectedItem);
        CloseRequested?.Invoke();
    }

    // 鼠标点击：分类卡片展开（同 → 键），普通项导航跳转并关闭
    public void Activate(SearchResultItem item)
    {
        if (item.IsHeader) return;

        if (item.IsCategoryCard)
        {
            SelectedItem = item;
            ExpandCategoryCard(item);
            return;
        }

        SelectedItem = item;
        _searchService.NavigateTo(item);
        CloseRequested?.Invoke();
    }

    // → 键：分类卡片展开为子项
    public void TryExpand()
    {
        if (SelectedItem != null && SelectedItem.IsCategoryCard)
        {
            ExpandCategoryCard(SelectedItem);
        }
    }

    // ← 键：已展开卡片下的子项收起回卡片
    public void TryCollapse()
    {
        if (SelectedItem == null || _expanded.Count == 0) return;
        var hostCard = FindHostCard(SelectedItem);
        if (hostCard != null) CollapseCategoryCard(hostCard);
    }

    public bool IsInsideExpandedCard(SearchResultItem item)
        => FindHostCard(item) != null;

    private SearchResultItem? FindHostCard(SearchResultItem item)
    {
        if (_expanded.Count == 0) return null;
        var idx = Results.IndexOf(item);
        if (idx < 0) return null;
        foreach (var kv in _expanded)
        {
            var (start, count) = kv.Value;
            if (idx >= start && idx < start + count) return kv.Key;
        }
        return null;
    }

    // 分类卡片行就地替换为该卡片下所有分类，选中第一项
    private void ExpandCategoryCard(SearchResultItem cardItem)
    {
        if (_expanded.ContainsKey(cardItem)) return;

        var children = _searchService.GetCategoryCardItems(cardItem);
        if (children.Count == 0) return;

        var index = Results.IndexOf(cardItem);
        if (index < 0) return;

        Results.RemoveAt(index);
        for (var i = 0; i < children.Count; i++)
        {
            Results.Insert(index + i, children[i]);
        }
        // 修正其他展开块的起点
        var delta = children.Count - 1;
        if (delta != 0)
        {
            var keys = new System.Collections.Generic.List<SearchResultItem>(_expanded.Keys);
            foreach (var k in keys)
            {
                var (s, c) = _expanded[k];
                if (s > index) _expanded[k] = (s + delta, c);
            }
        }
        _expanded[cardItem] = (index, children.Count);
        SelectedItem = children[0];
    }

    // 已展开的子项还原回卡片行
    private void CollapseCategoryCard(SearchResultItem cardItem)
    {
        if (!_expanded.TryGetValue(cardItem, out var range)) return;
        var (start, childCount) = range;
        if (start < 0 || start > Results.Count) return;

        // 移除子项
        for (var i = 0; i < childCount && start < Results.Count; i++)
        {
            Results.RemoveAt(start);
        }
        // 还原卡片
        Results.Insert(start, cardItem);
        // 修正其他展开块的起点
        var delta = childCount - 1;
        _expanded.Remove(cardItem);
        if (delta != 0)
        {
            var keys = new System.Collections.Generic.List<SearchResultItem>(_expanded.Keys);
            foreach (var k in keys)
            {
                var (s, c) = _expanded[k];
                if (s > start) _expanded[k] = (s - delta, c);
            }
        }
        SelectedItem = cardItem;
    }

    public void MoveSelection(int delta)
    {
        if (Results.Count == 0) return;
        // 全是 header 时不动选中
        bool anySelectable = false;
        for (var i = 0; i < Results.Count; i++)
        {
            if (!Results[i].IsHeader) { anySelectable = true; break; }
        }
        if (!anySelectable) return;

        var current = SelectedItem == null ? -1 : Results.IndexOf(SelectedItem);
        var step = delta == 0 ? 1 : Math.Sign(delta);
        var next = current;

        for (var guard = 0; guard < Results.Count; guard++)
        {
            next = current < 0
                ? (step > 0 ? 0 : Results.Count - 1)
                : Math.Clamp(next + step, 0, Results.Count - 1);

            // 已经触底/触顶且没找到可选行，停止
            if (current >= 0 && next == current) break;
            if (!Results[next].IsHeader) break;
            current = next;
        }

        if (next >= 0 && next < Results.Count && !Results[next].IsHeader)
        {
            SelectedItem = Results[next];
        }
    }

    private async Task LoadDefaultAsync()
    {
        try
        {
            var list = await _searchService.GetTodayHighlightsAsync();
            ApplyResults(list, isDefault: true, keywordIsEmpty: true);
        }
        catch
        {
            ApplyResults(Array.Empty<SearchResultItem>(), isDefault: true, keywordIsEmpty: true);
        }
    }

    public async Task RefreshAsync()
    {
        var keyword = (Keyword ?? string.Empty).Trim();
        if (keyword.Length == 0)
            await LoadDefaultAsync();
        else
            await OnKeywordChangedAsync(keyword);
    }

    private async Task OnKeywordChangedAsync(string keyword)
    {
        keyword = keyword?.Trim() ?? string.Empty;

        if (keyword.Length == 0)
        {
            await LoadDefaultAsync();
            return;
        }

        try
        {
            var list = await _searchService.SearchAsync(keyword);
            ApplyResults(list, isDefault: false, keywordIsEmpty: false);
        }
        catch
        {
            ApplyResults(Array.Empty<SearchResultItem>(), isDefault: false, keywordIsEmpty: false);
        }
    }

    private void ApplyResults(System.Collections.Generic.IReadOnlyList<SearchResultItem> list, bool isDefault, bool keywordIsEmpty)
    {
        _expanded.Clear();
        Results.Clear();
        foreach (var item in list) Results.Add(item);
        // 默认选中第一行可点击的（跳过 Header）
        SearchResultItem? first = null;
        for (var i = 0; i < Results.Count; i++)
        {
            if (!Results[i].IsHeader) { first = Results[i]; break; }
        }
        SelectedItem = first;
        IsShowingDefault = isDefault;
        IsEmpty = !keywordIsEmpty && Results.Count == 0;
        // 默认态不显示「快捷入口」提示文字，仅搜索结果态显示标题
        StatusText = isDefault ? string.Empty : ResourceStrings.SearchResults;
        // 计数仅算可点击的项
        var clickableCount = 0;
        for (var i = 0; i < Results.Count; i++)
        {
            if (!Results[i].IsHeader) clickableCount++;
        }
        ResultsCountText = clickableCount > 0
            ? string.Format(ResourceStrings.SearchResultCount, clickableCount)
            : string.Empty;
    }

    public override void Dispose()
    {
        _disposables.Dispose();
        base.Dispose();
    }
}

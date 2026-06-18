using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Taix.Client.Controls.Base;
using Taix.Client.Controls.Charts.Model;
using Taix.Client.Controls.Input;

namespace Taix.Client.Controls.Charts;


public class ListChart : TemplatedControl
{
    public static readonly DirectProperty<ListChart, IEnumerable<ChartsDataModel>> DataProperty =
        AvaloniaProperty.RegisterDirect<ListChart, IEnumerable<ChartsDataModel>>(
            nameof(Data), o => o.Data, (o, v) => o.Data = v);

    public static readonly DirectProperty<ListChart, bool> IsSearchProperty =
        AvaloniaProperty.RegisterDirect<ListChart, bool>(
            nameof(IsSearch), o => o.IsSearch, (o, v) => o.IsSearch = v);

    public static readonly DirectProperty<ListChart, bool> IsCanScrollProperty =
        AvaloniaProperty.RegisterDirect<ListChart, bool>(
            nameof(IsCanScroll), o => o.IsCanScroll, (o, v) => o.IsCanScroll = v);

    public static readonly DirectProperty<ListChart, ICommand> ClickCommandProperty =
        AvaloniaProperty.RegisterDirect<ListChart, ICommand>(
            nameof(ClickCommand), o => o.ClickCommand, (o, v) => o.ClickCommand = v);

    public static readonly DirectProperty<ListChart, ContextMenu> ItemMenuProperty =
        AvaloniaProperty.RegisterDirect<ListChart, ContextMenu>(
            nameof(ItemMenu), o => o.ItemMenu, (o, v) => o.ItemMenu = v);

    public static readonly DirectProperty<ListChart, bool> IsShowBadgeProperty =
        AvaloniaProperty.RegisterDirect<ListChart, bool>(
            nameof(IsShowBadge), o => o.IsShowBadge, (o, v) => o.IsShowBadge = v);

    public static readonly DirectProperty<ListChart, double> IconSizeProperty =
        AvaloniaProperty.RegisterDirect<ListChart, double>(
            nameof(IconSize), o => o.IconSize, (o, v) => o.IconSize = v);

    public static readonly DirectProperty<ListChart, double> DataMaxValueProperty =
        AvaloniaProperty.RegisterDirect<ListChart, double>(
            nameof(DataMaxValue), o => o.DataMaxValue, (o, v) => o.DataMaxValue = v);

    private static readonly TimeSpan SearchDebounceDelay = TimeSpan.FromMilliseconds(150);

    private IEnumerable<ChartsDataModel> _data = [];
    private bool _isSearch;
    private bool _isCanScroll = true;
    private ICommand? _clickCommand;
    private ContextMenu? _itemMenu;
    private bool _isShowBadge;
    private double _iconSize = 25;
    private double _dataMaxValue;
    private string _searchText = string.Empty;

    private ListBox _listView;
    private TextBox _searchBox;
    private Run _countText;
    private EmptyData _emptyDataView;
    private DispatcherTimer _searchTimer;
    private bool _templateApplied;
    private bool _handlersAttached;

    public event EventHandler? OnItemClick;

    public IEnumerable<ChartsDataModel> Data
    {
        get => _data;
        set => SetAndRaise(DataProperty, ref _data, value);
    }

    public bool IsSearch
    {
        get => _isSearch;
        set => SetAndRaise(IsSearchProperty, ref _isSearch, value);
    }

    public bool IsCanScroll
    {
        get => _isCanScroll;
        set => SetAndRaise(IsCanScrollProperty, ref _isCanScroll, value);
    }

    public ICommand ClickCommand
    {
        get => _clickCommand;
        set => SetAndRaise(ClickCommandProperty, ref _clickCommand, value);
    }

    public ContextMenu ItemMenu
    {
        get => _itemMenu;
        set => SetAndRaise(ItemMenuProperty, ref _itemMenu, value);
    }

    public bool IsShowBadge
    {
        get => _isShowBadge;
        set => SetAndRaise(IsShowBadgeProperty, ref _isShowBadge, value);
    }

    public double IconSize
    {
        get => _iconSize;
        set => SetAndRaise(IconSizeProperty, ref _iconSize, value);
    }

    public double DataMaxValue
    {
        get => _dataMaxValue;
        set => SetAndRaise(DataMaxValueProperty, ref _dataMaxValue, value);
    }

    protected override Type StyleKeyOverride => typeof(ListChart);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        DetachHandlers();

        _listView = e.NameScope.Get<ListBox>("ListView");
        _searchBox = e.NameScope.Get<TextBox>("SearchBox");
        _countText = e.NameScope.Get<Run>("CountText");
        _emptyDataView = e.NameScope.Find<EmptyData>("EmptyDataView");

        UpdateScrollability();
        RefreshDisplayData();

        _templateApplied = true;

        if (IsLoaded) AttachHandlers();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (!_templateApplied) return;

        if (change.Property == DataProperty)
            RefreshDisplayData();
        else if (change.Property == IsCanScrollProperty)
            UpdateScrollability();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        AttachHandlers();
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);

        StopSearchTimer();
        DetachHandlers();
    }

    private void AttachHandlers()
    {
        if (_handlersAttached) return;
        if (_listView == null && _searchBox == null) return;

        if (_listView != null)
        {
            _listView.PointerReleased += OnListViewPointerReleased;
            _listView.SelectionChanged += OnListSelectionChanged;
        }

        if (_searchBox != null)
            _searchBox.TextChanged += OnSearchTextChanged;

        _handlersAttached = true;
    }

    private void DetachHandlers()
    {
        if (!_handlersAttached) return;

        if (_listView != null)
        {
            _listView.PointerReleased -= OnListViewPointerReleased;
            _listView.SelectionChanged -= OnListSelectionChanged;
        }

        if (_searchBox != null)
            _searchBox.TextChanged -= OnSearchTextChanged;

        _handlersAttached = false;
    }


    private void RefreshDisplayData()
    {
        if (_listView == null) return;

        var source = Data as IReadOnlyList<ChartsDataModel> ?? Data?.ToList();
        if (source == null || source.Count == 0)
        {
            DataMaxValue = 0;
            if (_countText != null) _countText.Text = "0";
            _listView.ItemsSource = Array.Empty<ChartsDataModel>();
            if (_emptyDataView != null) _emptyDataView.IsVisible = true;
            return;
        }
        if (_emptyDataView != null) _emptyDataView.IsVisible = false;

        double maxValue = 0;
        for (var i = 0; i < source.Count; i++)
        {
            var v = source[i].Value;
            if (v > maxValue) maxValue = v;
        }

        if (maxValue < 10) maxValue = 10;
        DataMaxValue = Math.Round(maxValue / 2, MidpointRounding.AwayFromZero) * 2 + 2;

        if (_countText != null) _countText.Text = source.Count.ToString();

        if (string.IsNullOrEmpty(_searchText))
            _listView.ItemsSource = Data;
        else
            ApplySearch(_searchText);
    }

    private void UpdateScrollability()
    {
        if (_listView == null) return;

        if (!IsCanScroll)
            _listView.Classes.Add("noScroll");
        else
            _listView.Classes.Remove("noScroll");
    }

    #region Search

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        var text = (_searchBox as InputBox)?.Text ?? string.Empty;
        if (text == _searchText) return;
        _searchText = text;
        StartSearchTimer();
    }

    private void StartSearchTimer()
    {
        if (_searchTimer == null)
        {
            _searchTimer = new DispatcherTimer { Interval = SearchDebounceDelay };
            _searchTimer.Tick += OnSearchTimerTick;
        }

        _searchTimer.Stop();
        _searchTimer.Start();
    }

    private void StopSearchTimer()
    {
        _searchTimer?.Stop();
    }

    private void OnSearchTimerTick(object? sender, EventArgs e)
    {
        StopSearchTimer();
        ApplySearch(_searchText);
    }


    private void ApplySearch(string keyword)
    {
        if (_listView == null) return;

        var source = Data as IReadOnlyList<ChartsDataModel> ?? Data?.ToList();
        if (source == null || source.Count == 0)
        {
            _listView.ItemsSource = Array.Empty<ChartsDataModel>();
            if (_countText != null) _countText.Text = "0";
            return;
        }

        if (string.IsNullOrEmpty(keyword))
        {
            _listView.ItemsSource = Data;
            if (_countText != null) _countText.Text = source.Count.ToString();
            return;
        }

        var result = new List<ChartsDataModel>(Math.Min(source.Count, 16));
        for (var i = 0; i < source.Count; i++)
        {
            var item = source[i];
            if (MatchesSearch(item, keyword))
                result.Add(item);
        }

        _listView.ItemsSource = result;
        if (_countText != null) _countText.Text = result.Count.ToString();
    }

    private static bool MatchesSearch(ChartsDataModel item, string keyword)
    {
        if (string.IsNullOrEmpty(keyword)) return true;

        if ((item.Name?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false) ||
            (item.PopupText?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false))
            return true;

        var badges = item.BadgeList;
        if (badges == null) return false;

        // 针对 "忽略"/"ignore" 特殊处理：预先转小写一次，避免多次分配
        var lowerKeyword = keyword.ToLowerInvariant();
        if (ChartBadgeModel.IgnreLanguages.Contains(lowerKeyword))
        {
            for (var i = 0; i < badges.Count; i++)
                if (badges[i].Type == ChartBadgeType.Ignore) return true;
        }

        for (var i = 0; i < badges.Count; i++)
            if (badges[i].Name?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false)
                return true;

        return false;
    }

    #endregion

    #region Events

    private void OnListSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_listView?.ContextMenu != null && e.AddedItems.Count > 0)
            _listView.ContextMenu.Tag = e.AddedItems[0];
    }

    private void OnListViewPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton != MouseButton.Left || _listView?.SelectedItem == null) return;

        OnItemClick?.Invoke(_listView.SelectedItem, EventArgs.Empty);
        ClickCommand?.Execute(_listView.SelectedItem);
    }

    #endregion
}

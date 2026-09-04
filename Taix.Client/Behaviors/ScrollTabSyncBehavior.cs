using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Xaml.Interactivity;
using Taix.Client.Controls;
using TabbarControl = Taix.Client.Controls.Tabbar.Tabbar;

namespace Taix.Client.Behaviors;

public class ScrollTabSyncBehavior : Behavior<Control>
{
    public static readonly StyledProperty<string> TabbarNameProperty =
        AvaloniaProperty.Register<ScrollTabSyncBehavior, string>(nameof(TabbarName), "Tabbar");

    public static readonly StyledProperty<string> SectionPrefixProperty =
        AvaloniaProperty.Register<ScrollTabSyncBehavior, string>(nameof(SectionPrefix), "Section");

    public static readonly StyledProperty<string?> ScrollViewerNameProperty =
        AvaloniaProperty.Register<ScrollTabSyncBehavior, string?>(nameof(ScrollViewerName));

    // Sticky header 属性
    public static readonly StyledProperty<string?> StickyHeaderNameProperty =
        AvaloniaProperty.Register<ScrollTabSyncBehavior, string?>(nameof(StickyHeaderName));

    public static readonly StyledProperty<string?> StickyTitleTextNameProperty =
        AvaloniaProperty.Register<ScrollTabSyncBehavior, string?>(nameof(StickyTitleTextName));

    public string TabbarName
    {
        get => GetValue(TabbarNameProperty);
        set => SetValue(TabbarNameProperty, value);
    }

    public string SectionPrefix
    {
        get => GetValue(SectionPrefixProperty);
        set => SetValue(SectionPrefixProperty, value);
    }

    public string? ScrollViewerName
    {
        get => GetValue(ScrollViewerNameProperty);
        set => SetValue(ScrollViewerNameProperty, value);
    }

    public string? StickyHeaderName
    {
        get => GetValue(StickyHeaderNameProperty);
        set => SetValue(StickyHeaderNameProperty, value);
    }

    public string? StickyTitleTextName
    {
        get => GetValue(StickyTitleTextNameProperty);
        set => SetValue(StickyTitleTextNameProperty, value);
    }

    private ScrollSectionNavigator? _navigator;
    private ScrollViewer? _scrollViewer;
    private TabbarControl? _tabbar;
    private Border? _stickyHeader;
    private TextBlock? _stickyTitleText;

    protected override void OnAttached()
    {
        base.OnAttached();
        if (AssociatedObject is not null)
            AssociatedObject.Loaded += OnLoaded;
    }

    protected override void OnDetaching()
    {
        base.OnDetaching();
        if (AssociatedObject is not null)
            AssociatedObject.Loaded -= OnLoaded;
        _navigator?.Dispose();
        _navigator = null;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (AssociatedObject is null) return;

        ScrollViewer? scrollViewer = null;
        if (!string.IsNullOrEmpty(ScrollViewerName))
            scrollViewer = FindNamedControl<ScrollViewer>(AssociatedObject, ScrollViewerName);

        scrollViewer ??= FindParentScrollViewer(AssociatedObject);
        var tabbar = FindNamedControl<TabbarControl>(AssociatedObject, TabbarName);
        var sections = FindSections(AssociatedObject, SectionPrefix);

        if (scrollViewer is null || tabbar is null || sections.Count == 0) return;

        _scrollViewer = scrollViewer;
        _tabbar = tabbar;

        // 粘性标题
        if (!string.IsNullOrEmpty(StickyHeaderName))
            _stickyHeader = FindNamedControl<Border>(AssociatedObject, StickyHeaderName);
        if (!string.IsNullOrEmpty(StickyTitleTextName))
            _stickyTitleText = FindNamedControl<TextBlock>(AssociatedObject, StickyTitleTextName);

        _navigator = new ScrollSectionNavigator(
            scrollViewer,
            tabbar,
            sections,
            _stickyHeader is not null ? OnActiveIndexChanged : null);

        if (_stickyHeader is not null)
            OnActiveIndexChanged(_tabbar?.SelectedIndex ?? 0);
    }

    private void OnActiveIndexChanged(int activeIndex)
    {
        if (_stickyHeader is null || _scrollViewer is null || _tabbar is null) return;

        if (_stickyTitleText is not null)
        {
            var title = GetTabTitle(activeIndex);
            if (!string.IsNullOrEmpty(title))
                _stickyTitleText.Text = title;
        }

        _stickyHeader.Opacity = 1;
        _stickyHeader.RenderTransform = new TranslateTransform(0, 0);
    }

    private string? GetTabTitle(int index)
    {
        if (_tabbar?.Data == null || index < 0 || index >= _tabbar.Data.Count)
            return null;
        return _tabbar.Data[index];
    }

    private static ScrollViewer? FindParentScrollViewer(Control control)
    {
        var current = control.Parent;
        while (current is not null)
        {
            if (current is ScrollViewer sv) return sv;
            current = current.Parent;
        }
        return null;
    }

    private static T? FindNamedControl<T>(Control root, string name) where T : Control
    {
        var nameScope = NameScope.GetNameScope(root);
        return nameScope?.Find<T>(name);
    }

    private static List<Control> FindSections(Control root, string prefix)
    {
        var result = new List<Control>();
        var nameScope = NameScope.GetNameScope(root);
        if (nameScope is null) return result;

        for (int i = 0; ; i++)
        {
            var section = nameScope.Find<Control>($"{prefix}{i}");
            if (section is null) break;
            result.Add(section);
        }

        if (result.Count > 0) return result;

        var fallbackNames = new[] { "GeneralSection", "BehaviorSection", "DataSection", "AboutSection" };
        foreach (var name in fallbackNames)
        {
            var section = nameScope.Find<Control>(name);
            if (section is not null) result.Add(section);
        }

        return result;
    }
}

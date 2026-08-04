using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using TabbarControl = global::Taix.Client.Controls.Tabbar.Tabbar;

namespace Taix.Client.Controls;

public sealed class ScrollSectionNavigator : IDisposable
{
    public sealed record Options(double ActivationThresholdRatio = 0.15);

    private readonly ScrollViewer _scrollViewer;
    private readonly TabbarControl _tabbar;
    private readonly IReadOnlyList<Control> _sections;
    private readonly Options _options;
    private readonly Action<int>? _activeIndexChanged;
    private readonly double _stickyHeaderHeight;
    private bool _isUpdatingFromScroll;
    private bool _disposed;

    public ScrollSectionNavigator(
        ScrollViewer scrollViewer,
        TabbarControl tabbar,
        IReadOnlyList<Control> sections,
        Options? options = null,
        Action<int>? activeIndexChanged = null,
        double stickyHeaderHeight = 0)
    {
        _scrollViewer = scrollViewer ?? throw new ArgumentNullException(nameof(scrollViewer));
        _tabbar = tabbar ?? throw new ArgumentNullException(nameof(tabbar));
        _sections = sections ?? throw new ArgumentNullException(nameof(sections));
        _options = options ?? new Options();
        _activeIndexChanged = activeIndexChanged;
        _stickyHeaderHeight = Math.Max(0, stickyHeaderHeight);

        if (_sections.Count == 0)
            throw new ArgumentException("Sections list cannot be empty.", nameof(sections));

        _scrollViewer.ScrollChanged += OnScrollChanged;
        _tabbar.PropertyChanged += OnTabbarPropertyChanged;
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (_disposed) return;

        var activeIndex = CalculateActiveSection();
        if (activeIndex < 0 || activeIndex >= _sections.Count) return;

        _activeIndexChanged?.Invoke(activeIndex);

        if (activeIndex == _tabbar.SelectedIndex) return;

        _isUpdatingFromScroll = true;
        _tabbar.SelectedIndex = activeIndex;
        _isUpdatingFromScroll = false;
    }

    private void OnTabbarPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (_disposed) return;
        if (e.Property != TabbarControl.SelectedIndexProperty) return;
        if (_isUpdatingFromScroll) return;

        var newIndex = e.NewValue is int index ? index : -1;
        if (newIndex < 0 || newIndex >= _sections.Count) return;

        ScrollToSection(newIndex);
    }

    private int CalculateActiveSection()
    {
        var scrollOffset = _scrollViewer.Offset.Y;

        var content = _scrollViewer.Content as Visual;
        if (content == null) return 0;

        var threshold = _scrollViewer.Bounds.Height * _options.ActivationThresholdRatio;

        var activeIndex = 0;
        for (int i = 0; i < _sections.Count; i++)
        {
            var sectionTop = GetSectionTopRelativeToContent(_sections[i], content);
            if (!sectionTop.HasValue) continue;

            if (sectionTop.Value <= scrollOffset + threshold)
                activeIndex = i;
        }

        return activeIndex;
    }

    private void ScrollToSection(int index)
    {
        var content = _scrollViewer.Content as Visual;
        if (content == null) return;

        var targetTop = GetSectionTopRelativeToContent(_sections[index], content);
        if (!targetTop.HasValue) return;

        // 补偿吸附标题高度，使分区顶部完整露出在最顶层内容之下
        var targetY = Math.Max(0, targetTop.Value - _stickyHeaderHeight);
        _scrollViewer.Offset = new Vector(_scrollViewer.Offset.X, targetY);
    }

    private static double? GetSectionTopRelativeToContent(Control section, Visual content)
    {
        var point = section.TranslatePoint(new Point(0, 0), content);
        return point?.Y;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _scrollViewer.ScrollChanged -= OnScrollChanged;
        _tabbar.PropertyChanged -= OnTabbarPropertyChanged;
    }
}

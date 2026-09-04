using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using TabbarControl = global::Taix.Client.Controls.Tabbar.Tabbar;

namespace Taix.Client.Controls;

public sealed class ScrollSectionNavigator : IDisposable
{
    private const double SnapOffset = 25.0;

    private const double ActivationTolerance = 1.0;

    private readonly ScrollViewer _scrollViewer;
    private readonly TabbarControl _tabbar;
    private readonly IReadOnlyList<Control> _sections;
    private readonly Action<int>? _activeIndexChanged;

    private bool _isUpdatingFromScroll;

    private int _programmaticTarget = -1;

    private double _programmaticTargetOffset;

    private bool _disposed;

    public ScrollSectionNavigator(
        ScrollViewer scrollViewer,
        TabbarControl tabbar,
        IReadOnlyList<Control> sections,
        Action<int>? activeIndexChanged = null)
    {
        _scrollViewer = scrollViewer ?? throw new ArgumentNullException(nameof(scrollViewer));
        _tabbar = tabbar ?? throw new ArgumentNullException(nameof(tabbar));
        _sections = sections ?? throw new ArgumentNullException(nameof(sections));
        _activeIndexChanged = activeIndexChanged;

        if (_sections.Count == 0)
            throw new ArgumentException("Sections list cannot be empty.", nameof(sections));

        _scrollViewer.ScrollChanged += OnScrollChanged;
        _tabbar.PropertyChanged += OnTabbarPropertyChanged;
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (_disposed) return;

        if (_programmaticTarget >= 0)
        {
            _activeIndexChanged?.Invoke(_programmaticTarget);

            var reachedTarget = IsAtScrollEnd()
                || Math.Abs(_scrollViewer.Offset.Y - _programmaticTargetOffset) < ActivationTolerance;
            if (!reachedTarget) return;

            _programmaticTarget = -1;
            return;
        }

        var currentIndex = CalculateActiveSection();
        if (currentIndex < 0 || currentIndex >= _sections.Count) return;

        _activeIndexChanged?.Invoke(currentIndex);

        if (currentIndex == _tabbar.SelectedIndex) return;

        _isUpdatingFromScroll = true;
        _tabbar.SelectedIndex = currentIndex;
        _isUpdatingFromScroll = false;
    }

    private void OnTabbarPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (_disposed) return;
        if (e.Property != TabbarControl.SelectedIndexProperty) return;
        if (_isUpdatingFromScroll) return;

        var newIndex = e.NewValue is int index ? index : -1;
        if (newIndex < 0 || newIndex >= _sections.Count) return;

        _programmaticTarget = newIndex;
        _programmaticTargetOffset = ScrollToSection(newIndex);

        if (Math.Abs(_scrollViewer.Offset.Y - _programmaticTargetOffset) < ActivationTolerance)
            _programmaticTarget = -1;
    }

    private bool IsAtScrollEnd()
    {
        var maxOffset = Math.Max(0, _scrollViewer.Extent.Height - _scrollViewer.Viewport.Height);
        return _scrollViewer.Offset.Y >= maxOffset - ActivationTolerance;
    }

    private int CalculateActiveSection()
    {
        var scrollOffset = _scrollViewer.Offset.Y;

        var content = _scrollViewer.Content as Visual;
        if (content == null) return 0;

        // 兜底：已滚到底（Offset 被夹在 Extent - Viewport）时，最后一个分区无法
        // 让「顶部越过判定线」，直接选中它。
        var maxOffset = Math.Max(0, _scrollViewer.Extent.Height - _scrollViewer.Viewport.Height);
        if (maxOffset > 0 && scrollOffset >= maxOffset - ActivationTolerance)
            return _sections.Count - 1;

        var activeIndex = 0;
        for (int i = 0; i < _sections.Count; i++)
        {
            var sectionTop = GetSectionTopRelativeToContent(_sections[i], content);
            if (!sectionTop.HasValue) continue;

            if (sectionTop.Value <= scrollOffset + SnapOffset + ActivationTolerance)
                activeIndex = i;
        }

        return activeIndex;
    }

    private double ScrollToSection(int index)
    {
        var content = _scrollViewer.Content as Visual;
        if (content == null) return _scrollViewer.Offset.Y;

        var targetTop = GetSectionTopRelativeToContent(_sections[index], content);
        if (!targetTop.HasValue) return _scrollViewer.Offset.Y;

        var targetY = Math.Max(0, targetTop.Value - SnapOffset);

        _scrollViewer.Offset = new Vector(_scrollViewer.Offset.X, targetY);
        return targetY;
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

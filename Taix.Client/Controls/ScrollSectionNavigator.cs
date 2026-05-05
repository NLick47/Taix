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
    private bool _isUpdatingFromScroll;
    private bool _disposed;

    public ScrollSectionNavigator(
        ScrollViewer scrollViewer,
        TabbarControl tabbar,
        IReadOnlyList<Control> sections,
        Options? options = null)
    {
        _scrollViewer = scrollViewer ?? throw new ArgumentNullException(nameof(scrollViewer));
        _tabbar = tabbar ?? throw new ArgumentNullException(nameof(tabbar));
        _sections = sections ?? throw new ArgumentNullException(nameof(sections));
        _options = options ?? new Options();

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

        for (int i = 0; i < _sections.Count; i++)
        {
            var sectionTop = GetSectionTopRelativeToContent(_sections[i], content);
            if (!sectionTop.HasValue) continue;

            var sectionHeight = _sections[i].Bounds.Height;
            if (double.IsNaN(sectionHeight) || sectionHeight < 0)
                sectionHeight = 0;

            var sectionBottom = sectionTop.Value + sectionHeight;

            if (sectionBottom > scrollOffset)
                return i;
        }

        return _sections.Count - 1;
    }

    private void ScrollToSection(int index)
    {
        var content = _scrollViewer.Content as Visual;
        if (content == null) return;

        var targetTop = GetSectionTopRelativeToContent(_sections[index], content);
        if (!targetTop.HasValue) return;

        var targetY = Math.Max(0, targetTop.Value);
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

using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Avalonia.Xaml.Interactivity;

namespace Taix.Client.Behaviors;

public class PopupScrollDismissBehavior : Behavior<Popup>
{
    private IDisposable? _targetVisibleSubscription;
    private ScrollViewer? _scrollViewer;
    private Control? _topLevel;
    private Window? _window;

    protected override void OnAttached()
    {
        base.OnAttached();
        if (AssociatedObject != null)
        {
            AssociatedObject.Opened += OnPopupOpened;
            AssociatedObject.Closed += OnPopupClosed;
        }
    }

    protected override void OnDetaching()
    {
        base.OnDetaching();
        if (AssociatedObject != null)
        {
            AssociatedObject.Opened -= OnPopupOpened;
            AssociatedObject.Closed -= OnPopupClosed;
        }
        DetachHandlers();
    }

    private void OnPopupOpened(object? sender, EventArgs e)
    {
        AttachHandlers();
    }

    private void OnPopupClosed(object? sender, EventArgs e)
    {
        DetachHandlers();
    }

    private void AttachHandlers()
    {
        if (AssociatedObject == null) return;

        var placementTarget = AssociatedObject.PlacementTarget;

        _scrollViewer = FindParentScrollViewer(AssociatedObject);
        if (_scrollViewer != null)
            _scrollViewer.ScrollChanged += OnScrollChanged;

        var visibilityTarget = placementTarget ?? (AssociatedObject.Parent as Visual);
        if (visibilityTarget != null)
        {
            _targetVisibleSubscription = visibilityTarget.GetObservable(Visual.IsVisibleProperty)
                .Subscribe(isVisible =>
                {
                    if (!isVisible && AssociatedObject is { IsOpen: true })
                        AssociatedObject.IsOpen = false;
                });
        }

        _topLevel = TopLevel.GetTopLevel(AssociatedObject) as Control;
        if (_topLevel != null)
            _topLevel.AddHandler(InputElement.PointerPressedEvent, OnTopLevelPointerPressed, RoutingStrategies.Tunnel);

        _window = TopLevel.GetTopLevel(AssociatedObject) as Window;
        if (_window != null)
            _window.Deactivated += OnWindowDeactivated;
    }

    private void DetachHandlers()
    {
        if (_scrollViewer != null)
        {
            _scrollViewer.ScrollChanged -= OnScrollChanged;
            _scrollViewer = null;
        }

        _targetVisibleSubscription?.Dispose();
        _targetVisibleSubscription = null;

        if (_topLevel != null)
        {
            _topLevel.RemoveHandler(InputElement.PointerPressedEvent, OnTopLevelPointerPressed);
            _topLevel = null;
        }

        if (_window != null)
        {
            _window.Deactivated -= OnWindowDeactivated;
            _window = null;
        }
    }

    private void OnWindowDeactivated(object? sender, EventArgs e)
    {
        if (AssociatedObject is { IsOpen: true })
            AssociatedObject.IsOpen = false;
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (AssociatedObject is not { IsOpen: true }) return;
        if (_scrollViewer == null) return;

        var target = AssociatedObject.PlacementTarget ?? (AssociatedObject.Parent as Visual);
        if (target != null && !IsInViewport(target, _scrollViewer))
            AssociatedObject.IsOpen = false;
    }

    private void OnTopLevelPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (AssociatedObject is not { IsOpen: true }) return;

        var source = e.Source as Visual;
        if (source == null) return;

        if (IsDescendantOf(source, AssociatedObject.Child)) return;
        if (IsDescendantOf(source, AssociatedObject.PlacementTarget)) return;
        if (IsDescendantOf(source, AssociatedObject.Parent as Visual)) return;

        AssociatedObject.IsOpen = false;
    }

    private static bool IsInViewport(Visual target, ScrollViewer scrollViewer)
    {
        var t = target.TransformToVisual(scrollViewer);
        if (!t.HasValue) return false;

        var targetBounds = target.Bounds;
        var pos = t.Value.Transform(new Point(0, 0));
        var targetRect = new Rect(pos, targetBounds.Size);
        var viewport = new Rect(0, 0, scrollViewer.Viewport.Width, scrollViewer.Viewport.Height);

        return viewport.Intersects(targetRect);
    }

    private static ScrollViewer? FindParentScrollViewer(Popup popup)
    {
        var target = popup.PlacementTarget;
        if (target != null)
            return FindInVisualAncestors(target);

        var parent = popup.Parent as Visual;
        while (parent != null)
        {
            if (parent is ScrollViewer sv) return sv;
            var found = FindInVisualAncestors(parent);
            if (found != null) return found;
            parent = parent.Parent as Visual;
        }

        return null;
    }

    private static ScrollViewer? FindInVisualAncestors(Visual? visual)
    {
        var current = visual?.GetVisualParent();
        while (current != null)
        {
            if (current is ScrollViewer sv) return sv;
            current = current.GetVisualParent();
        }
        return null;
    }

    private static bool IsDescendantOf(Visual? node, Visual? ancestor)
    {
        if (node == null || ancestor == null) return false;
        if (ReferenceEquals(node, ancestor)) return true;

        var current = node.GetVisualParent();
        while (current != null)
        {
            if (ReferenceEquals(current, ancestor)) return true;
            current = current.GetVisualParent();
        }
        return false;
    }
}

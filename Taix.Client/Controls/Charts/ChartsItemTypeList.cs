using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Taix.Client.Controls.Base;
using Taix.Client.Controls.Charts.Model;

namespace Taix.Client.Controls.Charts;

public class ChartsItemTypeList : TemplatedControl
{
    public static readonly RoutedEvent<RoutedEventArgs> ContextMenuRequestedEvent =
        RoutedEvent.Register<ChartsItemTypeList, RoutedEventArgs>(nameof(ContextMenuRequested), RoutingStrategies.Bubble);

    public static readonly RoutedEvent<RoutedEventArgs> ItemClickRequestedEvent =
        RoutedEvent.Register<ChartsItemTypeList, RoutedEventArgs>(nameof(ItemClickRequested), RoutingStrategies.Bubble);

    public event EventHandler<RoutedEventArgs> ContextMenuRequested
    {
        add => AddHandler(ContextMenuRequestedEvent, value);
        remove => RemoveHandler(ContextMenuRequestedEvent, value);
    }

    public event EventHandler<RoutedEventArgs> ItemClickRequested
    {
        add => AddHandler(ItemClickRequestedEvent, value);
        remove => RemoveHandler(ItemClickRequestedEvent, value);
    }

    public static readonly DirectProperty<ChartsItemTypeList, ChartsDataModel> DataProperty =
        AvaloniaProperty.RegisterDirect<ChartsItemTypeList, ChartsDataModel>(
            nameof(Data),
            o => o.Data,
            (o, v) => o.Data = v);

    public static readonly DirectProperty<ChartsItemTypeList, double> MaxValueProperty =
        AvaloniaProperty.RegisterDirect<ChartsItemTypeList, double>(
            nameof(MaxValue),
            o => o.MaxValue,
            (o, v) => o.MaxValue = v);

    public static readonly DirectProperty<ChartsItemTypeList, bool> IsLoadingProperty =
        AvaloniaProperty.RegisterDirect<ChartsItemTypeList, bool>(
            nameof(IsLoading),
            o => o.IsLoading,
            (o, v) => o.IsLoading = v);

    public static readonly DirectProperty<ChartsItemTypeList, bool> IsShowBadgeProperty =
        AvaloniaProperty.RegisterDirect<ChartsItemTypeList, bool>(
            nameof(IsShowBadge),
            o => o.IsShowBadge,
            (o, v) => o.IsShowBadge = v);

    public static readonly DirectProperty<ChartsItemTypeList, double> IconSizeProperty =
        AvaloniaProperty.RegisterDirect<ChartsItemTypeList, double>(
            nameof(IconSize),
            o => o.IconSize,
            (o, v) => o.IconSize = v);

    private ChartsDataModel _data;

    private double _iconSize;

    private bool _isLoading;

    private bool _isShowBadge;

    private double _maxValue;
    private Img IconObj;
    private bool IsAddEvent;
    private bool isRendering;

    private TextBlock NameTextObj, ValueTextObj;
    private Rectangle ValueBlockObj;
    private StackPanel ValueContainer;
    private EventHandler? _layoutUpdatedHandler;

    public ChartsDataModel Data
    {
        get => _data;
        set => SetAndRaise(DataProperty, ref _data, value);
    }

    public double MaxValue
    {
        get => _maxValue;
        set => SetAndRaise(MaxValueProperty, ref _maxValue, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetAndRaise(IsLoadingProperty, ref _isLoading, value);
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

    protected override Type StyleKeyOverride => typeof(ChartsItemTypeList);


    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        PointerPressed -= OnPointerPressed;
        Loaded -= ChartsItemTypeList_Loaded;
        isRendering = false;
        var parent = Parent as Control;
        if (parent != null)
            parent.SizeChanged -= Parent_SizeChanged;
        if (_layoutUpdatedHandler != null)
        {
            ValueTextObj.LayoutUpdated -= _layoutUpdatedHandler;
            _layoutUpdatedHandler = null;
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        NameTextObj = e.NameScope.Get<TextBlock>("NameTextObj");
        ValueTextObj = e.NameScope.Get<TextBlock>("ValueTextObj");
        ValueBlockObj = e.NameScope.Get<Rectangle>("ValueBlockObj");
        ValueContainer = e.NameScope.Get<StackPanel>("ValueContainer");
        IconObj = e.NameScope.Get<Img>("IconObj");

        if (!IsAddEvent)
        {
            PointerPressed += OnPointerPressed;
            Loaded += ChartsItemTypeList_Loaded;
            IsAddEvent = true;
        }

        var parent = Parent as Control;
        if (parent != null)
        {
            parent.SizeChanged -= Parent_SizeChanged;
            parent.SizeChanged += Parent_SizeChanged;
        }
    }


    private void Parent_SizeChanged(object? sender, SizeChangedEventArgs e)
    {
        UpdateValueBlockWidth();
    }

    private void ChartsItemTypeList_Loaded(object? sender, RoutedEventArgs e)
    {
        Render();
    }

    private void Render()
    {
        if (isRendering || Data == null) return;
        isRendering = true;

        if (_layoutUpdatedHandler != null)
            ValueTextObj.LayoutUpdated -= _layoutUpdatedHandler;
        _layoutUpdatedHandler = (e, c) =>
        {
            if (MaxValue <= 0) return;
            UpdateValueBlockWidth();
        };
        ValueTextObj.LayoutUpdated += _layoutUpdatedHandler;

        ValueTextObj.Text = Data.Tag;
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(this);
        if (Data == null) return;

        if (point.Properties.IsLeftButtonPressed)
        {
            var args = new RoutedEventArgs(ItemClickRequestedEvent);
            RaiseEvent(args);
        }
        else if (point.Properties.IsRightButtonPressed)
        {
            var args = new RoutedEventArgs(ContextMenuRequestedEvent);
            RaiseEvent(args);
        }
    }

    public void UpdateValueBlockWidth()
    {
        if (Data == null || !IsLoaded) return;
        ValueBlockObj.Width = Data.Value / MaxValue * (ValueContainer.Bounds.Width * 0.95 - ValueTextObj.Bounds.Width);
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Taix.Client.Controls.Base;
using Taix.Client.Controls.Charts.Model;

namespace Taix.Client.Controls.Charts;

public class CardChart : TemplatedControl
{
    public static readonly DirectProperty<CardChart, IEnumerable<ChartsDataModel>> DataProperty =
        AvaloniaProperty.RegisterDirect<CardChart, IEnumerable<ChartsDataModel>>(
            nameof(Data), o => o.Data, (o, v) => o.Data = v);

    public static readonly DirectProperty<CardChart, int> ShowLimitProperty =
        AvaloniaProperty.RegisterDirect<CardChart, int>(
            nameof(ShowLimit), o => o.ShowLimit, (o, v) => o.ShowLimit = v);

    public static readonly DirectProperty<CardChart, double> MaxValueLimitProperty =
        AvaloniaProperty.RegisterDirect<CardChart, double>(
            nameof(MaxValueLimit), o => o.MaxValueLimit, (o, v) => o.MaxValueLimit = v);

    public static readonly DirectProperty<CardChart, ICommand> ClickCommandProperty =
        AvaloniaProperty.RegisterDirect<CardChart, ICommand>(
            nameof(ClickCommand), o => o.ClickCommand, (o, v) => o.ClickCommand = v);

    public static readonly DirectProperty<CardChart, ContextMenu> ItemMenuProperty =
        AvaloniaProperty.RegisterDirect<CardChart, ContextMenu>(
            nameof(ItemMenu), o => o.ItemMenu, (o, v) => o.ItemMenu = v);

    private IEnumerable<ChartsDataModel> _data = [];
    private int _showLimit;
    private double _maxValueLimit;
    private ICommand _clickCommand;
    private ContextMenu _itemMenu;
    private WrapPanel _cardContainer;
    private double _maxValue;
    private readonly HashSet<Control> _clickHandledControls = [];

    public event EventHandler? OnItemClick;

    public IEnumerable<ChartsDataModel> Data
    {
        get => _data;
        set => SetAndRaise(DataProperty, ref _data, value);
    }

    public int ShowLimit
    {
        get => _showLimit;
        set => SetAndRaise(ShowLimitProperty, ref _showLimit, value);
    }

    public double MaxValueLimit
    {
        get => _maxValueLimit;
        set => SetAndRaise(MaxValueLimitProperty, ref _maxValueLimit, value);
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

    protected override Type StyleKeyOverride => typeof(CardChart);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _cardContainer = e.NameScope.Get<WrapPanel>("CardContainer");
        Render();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DataProperty)
            Render();
        else if (change.Property == ItemMenuProperty && _cardContainer != null)
            _cardContainer.ContextMenu = _itemMenu;
    }

    protected override void OnUnloaded(Avalonia.Interactivity.RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        foreach (var kvp in _clickHandledControls.ToList())
        {
            if (kvp != null) kvp.PointerPressed -= OnItemPointerPressed;
        }
        _clickHandledControls.Clear();
    }

    private void Render()
    {
        if (_cardContainer == null) return;

        var list = Data?.ToList();
        if (list == null || list.Count == 0)
        {
            _cardContainer.Children.Clear();
            _cardContainer.Children.Add(new EmptyData());
            return;
        }

        _maxValue = MaxValueLimit > 0 ? MaxValueLimit : list.Max(m => m.Value);
        var data = list;

        _cardContainer.ContextMenu = _itemMenu;

        _cardContainer.Children.Clear();
        foreach (var item in data)
        {
            var card = new ChartsItemTypeCard
            {
                Data = item,
                MaxValue = _maxValue
            };
            ToolTip.SetTip(card, item.Name);
            HandleItemClick(card, item);
            _cardContainer.Children.Add(card);
        }
    }

    private void HandleItemClick(Control el, ChartsDataModel data)
    {
        if (!_clickHandledControls.Add(el)) return;
        el.PointerPressed += OnItemPointerPressed;
    }

    private void OnItemPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var el = sender as Control;
        if (el == null) return;
        var clickData = (el as ChartsItemTypeCard)?.Data;
        if (clickData == null) return;

        if (e.GetCurrentPoint(el).Properties.IsLeftButtonPressed)
        {
            OnItemClick?.Invoke(clickData, EventArgs.Empty);
            ClickCommand?.Execute(clickData);
        }
        if (e.GetCurrentPoint(el).Properties.IsRightButtonPressed && ItemMenu != null)
        {
            ItemMenu.Tag = clickData;
        }
    }
}

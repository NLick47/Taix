using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Taix.Client.Controls.Base;
using Taix.Client.Controls.Charts.Model;
using Taix.Client.Servicers;

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

    public static readonly DirectProperty<CardChart, ContextMenuType> MenuTypeProperty =
        AvaloniaProperty.RegisterDirect<CardChart, ContextMenuType>(
            nameof(MenuType), o => o.MenuType, (o, v) => o.MenuType = v);

    public static readonly DirectProperty<CardChart, double> IconSizeProperty =
        AvaloniaProperty.RegisterDirect<CardChart, double>(
            nameof(IconSize), o => o.IconSize, (o, v) => o.IconSize = v);

    private IEnumerable<ChartsDataModel> _data = [];
    private int _showLimit;
    private double _maxValueLimit;
    private ICommand _clickCommand;
    private ContextMenuType _menuType;
    private double _iconSize = 32;
    private WrapPanel _cardContainer;
    private double _maxValue;

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

    public ContextMenuType MenuType
    {
        get => _menuType;
        set => SetAndRaise(MenuTypeProperty, ref _menuType, value);
    }

    public double IconSize
    {
        get => _iconSize;
        set => SetAndRaise(IconSizeProperty, ref _iconSize, value);
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
    }

    protected override void OnUnloaded(Avalonia.Interactivity.RoutedEventArgs e)
    {
        base.OnUnloaded(e);
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

        _cardContainer.Children.Clear();
        foreach (var item in data)
        {
            var card = new ChartsItemTypeCard
            {
                Data = item,
                MaxValue = _maxValue,
                IconSize = IconSize
            };
            ToolTip.SetTip(card, item.Name);
            card.PointerPressed += OnItemPointerPressed;
            _cardContainer.Children.Add(card);
        }
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
        else if (e.GetCurrentPoint(el).Properties.IsRightButtonPressed)
        {
            ShowContextMenu(clickData, el, e);
        }
    }

    private async void ShowContextMenu(ChartsDataModel data, Control target, PointerPressedEventArgs e)
    {
        var menu = await CreateMenuForDataAsync(data);
        if (menu == null) return;

        target.ContextMenu = menu;
        menu.Closed += OnCardContextMenuClosed;
        menu.Open(target);
    }

    private void OnCardContextMenuClosed(object? sender, RoutedEventArgs e)
    {
        if (sender is ContextMenu menu)
        {
            menu.Closed -= OnCardContextMenuClosed;
            if (menu.PlacementTarget is Control target)
                target.ContextMenu = null;
        }
    }

    private async Task<ContextMenu?> CreateMenuForDataAsync(ChartsDataModel data)
    {
        var servicer = ServiceLocator.GetService<IContextMenuServicer>();
        return servicer == null ? null : await servicer.CreateContextMenuAsync(MenuType, data);
    }
}

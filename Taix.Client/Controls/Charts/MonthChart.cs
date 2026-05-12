using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Taix.Client.Controls.Charts.Model;

namespace Taix.Client.Controls.Charts;

public class MonthChart : TemplatedControl
{
    public static readonly DirectProperty<MonthChart, IEnumerable<ChartsDataModel>> DataProperty =
        AvaloniaProperty.RegisterDirect<MonthChart, IEnumerable<ChartsDataModel>>(
            nameof(Data), o => o.Data, (o, v) => o.Data = v);

    public static readonly DirectProperty<MonthChart, int> ShowLimitProperty =
        AvaloniaProperty.RegisterDirect<MonthChart, int>(
            nameof(ShowLimit), o => o.ShowLimit, (o, v) => o.ShowLimit = v);

    public static readonly DirectProperty<MonthChart, ICommand> ClickCommandProperty =
        AvaloniaProperty.RegisterDirect<MonthChart, ICommand>(
            nameof(ClickCommand), o => o.ClickCommand, (o, v) => o.ClickCommand = v);

    public static readonly DirectProperty<MonthChart, ContextMenu> ItemMenuProperty =
        AvaloniaProperty.RegisterDirect<MonthChart, ContextMenu>(
            nameof(ItemMenu), o => o.ItemMenu, (o, v) => o.ItemMenu = v);

    private IEnumerable<ChartsDataModel> _data = [];
    private int _showLimit;
    private ICommand _clickCommand;
    private ContextMenu _itemMenu;
    private Grid _monthContainer;
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

    protected override Type StyleKeyOverride => typeof(MonthChart);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _monthContainer = e.NameScope.Get<Grid>("MonthContainer");
        Render();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DataProperty) Render();
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
        if (_monthContainer == null) return;
        _monthContainer.Children.Clear();

        var list = Data?.ToList();
        if (list == null || list.Count == 0) return;

        var month = list[0].DateTime;
        var days = DateTime.DaysInMonth(month.Year, month.Month);
        _maxValue = list.Count > 0 ? list.Max(m => m.Value) : 0;

        string[] week =
        [
            Application.Current?.Resources["Mon"] as string ?? "Mon",
            Application.Current?.Resources["Tue"] as string ?? "Tue",
            Application.Current?.Resources["Wed"] as string ?? "Wed",
            Application.Current?.Resources["Thu"] as string ?? "Thu",
            Application.Current?.Resources["Fri"] as string ?? "Fri",
            Application.Current?.Resources["Sat"] as string ?? "Sat",
            Application.Current?.Resources["Sun"] as string ?? "Sun"
        ];

        var headerGrid = new Grid { Margin = new Thickness(0, 0, 0, 10) };
        var dataGrid = new Grid();

        for (var i = 0; i < 7; i++)
        {
            dataGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var header = new TextBlock
            {
                Text = week[i],
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            Grid.SetColumn(header, i);
            headerGrid.Children.Add(header);
        }

        for (var i = 0; i < 6; i++)
            dataGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // empty placeholders
        for (var i = 0; i < days; i++)
        {
            var date = new DateTime(month.Year, month.Month, i + 1);
            var chartsItem = new ChartsItemTypeMonth { Data = new ChartsDataModel { DateTime = date } };
            var location = CalGridLocation(date);
            Grid.SetColumn(chartsItem, location[0]);
            Grid.SetRow(chartsItem, location[1]);
            dataGrid.Children.Add(chartsItem);
        }

        // data items
        foreach (var item in list)
        {
            var chartsItem = new ChartsItemTypeMonth
            {
                Data = item,
                MaxValue = _maxValue
            };
            ToolTip.SetTip(chartsItem, item.PopupText);
            HandleItemClick(chartsItem, item);

            var location = CalGridLocation(item.DateTime);
            Grid.SetColumn(chartsItem, location[0]);
            Grid.SetRow(chartsItem, location[1]);
            dataGrid.Children.Add(chartsItem);
        }

        var sp = new StackPanel();
        sp.Children.Add(headerGrid);
        sp.Children.Add(dataGrid);
        _monthContainer.Children.Add(sp);
    }

    private static int[] CalGridLocation(DateTime date)
    {
        var res = new int[2];
        var firstDayWeekNum = (int)new DateTime(date.Year, date.Month, 1).DayOfWeek;
        if (firstDayWeekNum == 0) firstDayWeekNum = 7;

        int col;
        if (firstDayWeekNum == 1)
            col = date.Day - 1;
        else
            col = date.Day + firstDayWeekNum - 2;

        var row = 0;
        if (col > 6)
        {
            row = col / 7;
            var dayWeekNum = (int)new DateTime(date.Year, date.Month, date.Day).DayOfWeek;
            if (dayWeekNum == 0) dayWeekNum = 7;
            col = dayWeekNum - 1;
        }

        res[0] = col;
        res[1] = row;
        return res;
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
        var clickData = (el as ChartsItemTypeMonth)?.Data;
        if (clickData == null) return;

        if (e.GetCurrentPoint(el).Properties.IsLeftButtonPressed)
        {
            OnItemClick?.Invoke(clickData, EventArgs.Empty);
            ClickCommand?.Execute(clickData);
        }
        if (e.GetCurrentPoint(el).Properties.IsRightButtonPressed && ItemMenu != null)
            ItemMenu.Tag = clickData;
    }
}

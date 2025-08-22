using System;
using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace UI.Controls.Select;

public class Option : TemplatedControl
{
    public static readonly StyledProperty<bool> IsShowIconProperty =
        AvaloniaProperty.Register<Option, bool>(nameof(IsShowIcon));

    public static readonly StyledProperty<bool> IsCheckedProperty =
        AvaloniaProperty.Register<Option, bool>(nameof(IsChecked));

    public static readonly StyledProperty<SelectItemModel> ValueProperty =
        AvaloniaProperty.Register<Option, SelectItemModel>(nameof(Value));

    public Option()
    {
        PointerPressed += OnPointerPressed;
    }

    public bool IsShowIcon
    {
        get => GetValue(IsShowIconProperty);
        set => SetValue(IsShowIconProperty, value);
    }

    public bool IsChecked
    {
        get => GetValue(IsCheckedProperty);
        set => SetValue(IsCheckedProperty, value);
    }

    public SelectItemModel Value
    {
        get => (SelectItemModel)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }


    protected override Type StyleKeyOverride => typeof(Option);

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        PointerPressed -= OnPointerPressed;
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        IsChecked = !IsChecked;
    }
}
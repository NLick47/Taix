using System;
using Avalonia;
using Avalonia.Controls.Primitives;

namespace Taix.Client.Controls.DatePickerBar;

public class DatePickerGridItem : TemplatedControl
{
    public static readonly DirectProperty<DatePickerGridItem, string> TitleProperty =
        AvaloniaProperty.RegisterDirect<DatePickerGridItem, string>(
            nameof(Title),
            o => o.Title,
            (o, v) => o.Title = v);

    public static readonly DirectProperty<DatePickerGridItem, int> ValueProperty =
        AvaloniaProperty.RegisterDirect<DatePickerGridItem, int>(
            nameof(Value),
            o => o.Value,
            (o, v) => o.Value = v);

    public static readonly DirectProperty<DatePickerGridItem, bool> IsSelectedProperty =
        AvaloniaProperty.RegisterDirect<DatePickerGridItem, bool>(
            nameof(IsSelected),
            o => o.IsSelected,
            (o, v) => o.IsSelected = v);

    private string _title = string.Empty;
    private int _value;
    private bool _isSelected;

    public string Title
    {
        get => _title;
        set => SetAndRaise(TitleProperty, ref _title, value);
    }

    public int Value
    {
        get => _value;
        set => SetAndRaise(ValueProperty, ref _value, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetAndRaise(IsSelectedProperty, ref _isSelected, value);
    }

    protected override Type StyleKeyOverride => typeof(DatePickerGridItem);
}

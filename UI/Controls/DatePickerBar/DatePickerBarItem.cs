using System;
using Avalonia;
using Avalonia.Controls.Primitives;

namespace UI.Controls.DatePickerBar;

public class DatePickerBarItem : TemplatedControl
{
    public static readonly DirectProperty<DatePickerBarItem, string> TitleProperty =
        AvaloniaProperty.RegisterDirect<DatePickerBarItem, string>(nameof(IsSelected),
            o => o.Title, (o, v) => o.Title = v);

    public static readonly DirectProperty<DatePickerBarItem, bool> IsSelectedProperty =
        AvaloniaProperty.RegisterDirect<DatePickerBarItem, bool>(nameof(IsSelected),
            o => o.IsSelected, (o, v) => o.IsSelected = v);

    public static readonly StyledProperty<bool> IsDisabledProperty =
        AvaloniaProperty.Register<DatePickerBarItem, bool>(nameof(IsDisabled));


    public static readonly DirectProperty<DatePickerBarItem, DateTime> DateProperty =
        AvaloniaProperty.RegisterDirect<DatePickerBarItem, DateTime>(
            nameof(Date),
            o => o.Date,
            (o, v) => o.Date = v);

    private DateTime _date;


    private bool _isSelected;
    private string _title = string.Empty;

    public string Title
    {
        get => _title;
        set => SetAndRaise(TitleProperty, ref _title, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetAndRaise(IsSelectedProperty, ref _isSelected, value);
    }

    public bool IsDisabled
    {
        get => GetValue(IsDisabledProperty);
        set => SetValue(IsDisabledProperty, value);
    }


    public DateTime Date
    {
        get => _date;
        set => SetAndRaise(DateProperty, ref _date, value);
    }

    protected override Type StyleKeyOverride => typeof(DatePickerBarItem);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DateProperty && change.NewValue != change.OldValue)
        {
            var control = change.Sender as DatePickerBarItem;
            control.IsDisabled = control.Date > DateTime.Now.Date;
        }
    }
}
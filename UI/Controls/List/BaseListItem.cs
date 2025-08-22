using System;
using Avalonia;
using Avalonia.Controls.Primitives;

namespace UI.Controls.List;

public class BaseListItem : TemplatedControl
{
    public static readonly DirectProperty<BaseListItem, string> TextProperty =
        AvaloniaProperty.RegisterDirect<BaseListItem, string>(
            nameof(Text),
            o => o.Text,
            (o, v) => o.Text = v);

    public static readonly DirectProperty<BaseListItem, bool> IsSelectedProperty =
        AvaloniaProperty.RegisterDirect<BaseListItem, bool>(
            nameof(IsSelected),
            o => o.IsSelected,
            (o, v) => o.IsSelected = v);

    private bool _isSelected;
    private string _text = string.Empty;

    public string Text
    {
        get => _text;
        set => SetAndRaise(TextProperty, ref _text, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetAndRaise(IsSelectedProperty, ref _isSelected, value);
    }

    protected override Type StyleKeyOverride => typeof(BaseListItem);
}
using System;
using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Input;

namespace UI.Controls.Toggle;

public class Toggle : TemplatedControl
{
    public static readonly StyledProperty<bool> IsCheckedProperty =
        AvaloniaProperty.Register<Toggle, bool>(nameof(IsChecked));

    public static readonly StyledProperty<ToggleTextPosition> TextPositionProperty =
        AvaloniaProperty.Register<Toggle, ToggleTextPosition>(nameof(IsChecked), ToggleTextPosition.Right);

    public static readonly StyledProperty<string> OnTextProperty =
        AvaloniaProperty.Register<Toggle, string>(nameof(OnText), "On");

    public static readonly StyledProperty<string> OffTextProperty =
        AvaloniaProperty.Register<Toggle, string>(nameof(OffText), "Off");

    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<Toggle, string>(nameof(Text));

    public bool IsChecked
    {
        get => GetValue(IsCheckedProperty);
        set => SetValue(IsCheckedProperty, value);
    }

    public ToggleTextPosition TextPosition
    {
        get => GetValue(TextPositionProperty);
        set => SetValue(TextPositionProperty, value);
    }

    public string OnText
    {
        get => GetValue(OnTextProperty);
        set => SetValue(OnTextProperty, value);
    }

    public string OffText
    {
        get => (string)GetValue(OffTextProperty);
        set => SetValue(OffTextProperty, value);
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(Toggle);
    public event EventHandler ToggleChanged;

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        IsChecked = !IsChecked;
        ToggleChanged?.Invoke(this, EventArgs.Empty);
    }
}
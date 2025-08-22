using System;
using Avalonia;
using Avalonia.Controls.Primitives;
using UI.Controls.Base;

namespace UI.Controls.Button;

public class Button : Avalonia.Controls.Button
{
    public static readonly StyledProperty<IconTypes> IconProperty =
        AvaloniaProperty.Register<Button, IconTypes>(nameof(Icon));

    public static readonly DirectProperty<Button, string> TextProperty =
        AvaloniaProperty.RegisterDirect<Button, string>(
            nameof(Text),
            o => o.Text,
            (o, v) => o.Text = v);

    public static readonly DirectProperty<Button, bool> ValueProperty =
        AvaloniaProperty.RegisterDirect<Button, bool>(
            nameof(Value),
            o => o.Value,
            (o, v) => o.Value = v);

    private string _text;

    private bool _value;

    public IconTypes Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public string Text
    {
        get => _text;
        set => SetAndRaise(TextProperty, ref _text, value);
    }

    public bool Value
    {
        get => _value;
        set => SetAndRaise(ValueProperty, ref _value, value);
    }


    protected override Type StyleKeyOverride => typeof(Button);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty)
        {
            var button = change.Sender as Button;
            button?.SetContent();
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        SetContent();
    }

    private void SetContent()
    {
        if (string.IsNullOrEmpty(Text) || Text.IndexOf('?') == -1 || Text.IndexOf(':') == -1) return;

        var yes = Text.Substring(Text.IndexOf('?') + 1, Text.IndexOf(':') - 1);
        var no = Text.Substring(Text.IndexOf(':') + 1);
        Content = Value ? yes : no;
    }
}
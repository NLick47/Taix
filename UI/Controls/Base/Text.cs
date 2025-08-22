using System;
using Avalonia;
using Avalonia.Controls.Primitives;

namespace UI.Controls.Base;

public class Text : TemplatedControl
{
    public static readonly DirectProperty<Text, string> ContentProperty =
        AvaloniaProperty.RegisterDirect<Text, string>(
            nameof(Content),
            o => o.Content,
            (o, v) => o.Content = v);

    public static readonly DirectProperty<Text, bool> ValueProperty =
        AvaloniaProperty.RegisterDirect<Text, bool>(
            nameof(Value),
            o => o.Value,
            (o, v) => o.Value = v);

    public static readonly DirectProperty<Text, string> TextValueProperty =
        AvaloniaProperty.RegisterDirect<Text, string>(
            nameof(TextValue),
            o => o.TextValue,
            (o, v) => o.TextValue = v);

    private string _content;

    private string _textValue;

    private bool _value;

    public string Content
    {
        get => _content;
        set => SetAndRaise(ContentProperty, ref _content, value);
    }

    public bool Value
    {
        get => _value;
        set => SetAndRaise(ValueProperty, ref _value, value);
    }

    public string TextValue
    {
        get => _textValue;
        set => SetAndRaise(TextValueProperty, ref _textValue, value);
    }

    protected override Type StyleKeyOverride => typeof(Text);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty)
        {
            var c = change.Sender as Text;
            c.SetContent();
        }
    }


    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        SetContent();
    }

    private void SetContent()
    {
        if (string.IsNullOrEmpty(Content) || Content.IndexOf('?') == -1 || Content.IndexOf(':') == -1) return;

        var yes = Content.Substring(Content.IndexOf('?') + 1, Content.IndexOf(':') - 1);
        var no = Content.Substring(Content.IndexOf(':') + 1);
        TextValue = Value ? yes : no;
    }
}
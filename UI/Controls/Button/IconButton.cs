using System;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using UI.Controls.Base;

namespace UI.Controls.Button;

public class IconButton : ContentControl
{
    public static readonly DirectProperty<IconButton, ICommand?> CommandProperty =
        AvaloniaProperty.RegisterDirect<IconButton, ICommand?>(
            nameof(Command),
            o => o.Command,
            (o, v) => o.Command = v,
            enableDataValidation: true);

    public static readonly DirectProperty<IconButton, object> CommandParameterProperty =
        AvaloniaProperty.RegisterDirect<IconButton, object>(
            nameof(CommandParameter),
            o => o.CommandParameter,
            (o, v) => o.CommandParameter = v);

    public static readonly StyledProperty<IconTypes> IconProperty =
        AvaloniaProperty.Register<IconButton, IconTypes>(nameof(Icon));

    private ICommand? _command;

    private object _commandParameter;

    public ICommand? Command
    {
        get => _command;
        set => SetAndRaise(CommandProperty, ref _command, value);
    }

    public object CommandParameter
    {
        get => _commandParameter;
        set => SetAndRaise(CommandParameterProperty, ref _commandParameter, value);
    }

    public IconTypes Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }


    protected override Type StyleKeyOverride => typeof(IconButton);

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Command?.Execute(CommandParameter);
    }
}
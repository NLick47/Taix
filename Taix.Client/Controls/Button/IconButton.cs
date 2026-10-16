using System;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Taix.Client.Controls.Base;

namespace Taix.Client.Controls.Button;

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

    public static readonly StyledProperty<Geometry?> PathDataProperty =
        AvaloniaProperty.Register<IconButton, Geometry?>(nameof(PathData));

    public static readonly StyledProperty<double> PathStrokeThicknessProperty =
        AvaloniaProperty.Register<IconButton, double>(nameof(PathStrokeThickness));

    public static readonly StyledProperty<bool> HandlesPointerPressedProperty =
        AvaloniaProperty.Register<IconButton, bool>(nameof(HandlesPointerPressed));

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

    public Geometry? PathData
    {
        get => GetValue(PathDataProperty);
        set => SetValue(PathDataProperty, value);
    }

    public double PathStrokeThickness
    {
        get => GetValue(PathStrokeThicknessProperty);
        set => SetValue(PathStrokeThicknessProperty, value);
    }

    public bool HandlesPointerPressed
    {
        get => GetValue(HandlesPointerPressedProperty);
        set => SetValue(HandlesPointerPressedProperty, value);
    }


    protected override Type StyleKeyOverride => typeof(IconButton);

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Command?.Execute(CommandParameter);
        if (HandlesPointerPressed) e.Handled = true;
    }
}

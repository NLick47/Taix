using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using System;
using System.Windows.Input;
using UI.Controls.Base;

namespace UI.Controls.Button
{
    public class IconButton : ContentControl
    {
        private ICommand? _command;
        public ICommand? Command
        {
            get => _command;
            set => SetAndRaise(CommandProperty, ref _command, value);
        }
        public static readonly DirectProperty<IconButton, ICommand?> CommandProperty =
            AvaloniaProperty.RegisterDirect<IconButton, ICommand?>(
                nameof(Command),
                o => o.Command,
                (o, v) => o.Command = v,
                enableDataValidation: true);

        private object _commandParameter;
        public object CommandParameter
        {
            get => _commandParameter;
            set => SetAndRaise(CommandParameterProperty, ref _commandParameter, value);
        }
        public static readonly DirectProperty<IconButton, object> CommandParameterProperty =
            AvaloniaProperty.RegisterDirect<IconButton, object>(
                nameof(CommandParameter),
                o => o.CommandParameter,
                (o, v) => o.CommandParameter = v);

        public IconTypes Icon
        {
            get { return GetValue(IconProperty); }
            set { SetValue(IconProperty, value); }
        }

        public static readonly StyledProperty<IconTypes> IconProperty =
          AvaloniaProperty.Register<IconButton,IconTypes>(nameof(Icon));


        protected override Type StyleKeyOverride => typeof(IconButton);

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            Command?.Execute(CommandParameter);
        }

     
    }
}

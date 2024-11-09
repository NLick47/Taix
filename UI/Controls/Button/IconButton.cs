using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Markup.Xaml.Templates;
using Microsoft.Extensions.DependencyInjection;
using NPOI.XSSF.Streaming.Properties;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using UI.Controls.Base;

namespace UI.Controls.Button
{
    public class IconButton : TemplatedControl
    {
        public ICommand Command
        {
            get { return (ICommand)GetValue(CommandProperty); }
            set { SetValue(CommandProperty, value); }
        }

        public static readonly StyledProperty<ICommand> CommandProperty =
           AvaloniaProperty.Register<IconButton, ICommand>(nameof(Command));

        public object CommandParameter
        {
            get { return (object)GetValue(CommandParameterProperty); }
            set { SetValue(CommandParameterProperty, value); }
        }

        public static readonly StyledProperty<object> CommandParameterProperty =
           AvaloniaProperty.Register<IconButton,object>(nameof(CommandParameter));

        public IconTypes Icon
        {
            get { return (IconTypes)GetValue(IconProperty); }
            set { SetValue(IconProperty, value); }
        }

        public static readonly StyledProperty<IconTypes> IconProperty =
          AvaloniaProperty.Register<IconButton,IconTypes>(nameof(Icon));

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            Command?.Execute(CommandParameter);
        }
    }
}

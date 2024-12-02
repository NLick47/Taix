using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UI.Controls.Base;

namespace UI.Controls.Button
{
    public class Button : Avalonia.Controls.Button
    {
        public IconTypes Icon
        {
            get { return (IconTypes)GetValue(IconProperty); }
            set { SetValue(IconProperty, value); }
        }

        public static readonly StyledProperty<IconTypes> IconProperty =
            AvaloniaProperty.Register<Button, IconTypes>(nameof(Icon), IconTypes.None);

        public string Text
        {
            get { return (string)GetValue(TextProperty); }
            set { SetValue(TextProperty, value); }
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if(change.Property == ContentProperty)
            {
                var button = change.Sender as Button;
                button?.SetContent();
            }
        }

        public static readonly StyledProperty<string> TextProperty =
            AvaloniaProperty.Register<Button, string>(nameof(Text));

        public bool Value
        {
            get { return (bool)GetValue(ValueProperty); }
            set { SetValue(ValueProperty, value); }
        }

        public static readonly StyledProperty<bool> ValueProperty =
           AvaloniaProperty.Register<Button, bool>(nameof(Value));

        protected override Type StyleKeyOverride => typeof(Button);

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            SetContent();
            
        }

        private void SetContent()
        {
            if (string.IsNullOrEmpty(Text) || Text.IndexOf('?') == -1 || Text.IndexOf(':') == -1)
            {
                return;
            }

            string yes = Text.Substring(Text.IndexOf('?') + 1, Text.IndexOf(':') - 1);
            string no = Text.Substring(Text.IndexOf(':') + 1);
            Content = Value ? yes : no;
        }
    }
}

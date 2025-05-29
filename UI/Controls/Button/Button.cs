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
            get { return GetValue(IconProperty); }
            set { SetValue(IconProperty, value); }
        }

        public static readonly StyledProperty<IconTypes> IconProperty =
            AvaloniaProperty.Register<Button, IconTypes>(nameof(Icon), IconTypes.None);

        private string _text;
        public string Text
        {
            get => _text;
            set => SetAndRaise(TextProperty, ref _text, value);
        }
        public static readonly DirectProperty<Button, string> TextProperty =
            AvaloniaProperty.RegisterDirect<Button, string>(
                nameof(Text),
                o => o.Text,
                (o, v) => o.Text = v);

        private bool _value;
        public bool Value
        {
            get => _value;
            set => SetAndRaise(ValueProperty, ref _value, value);
        }
        public static readonly DirectProperty<Button, bool> ValueProperty =
            AvaloniaProperty.RegisterDirect<Button, bool>(
                nameof(Value),
                o => o.Value,
                (o, v) => o.Value = v);

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if(change.Property == ValueProperty)
            {
                var button = change.Sender as Button;
                button?.SetContent();
            }
        }
        

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

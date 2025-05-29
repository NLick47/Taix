using Avalonia;
using Avalonia.Controls.Primitives;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UI.Controls.Base
{
    public class Text : TemplatedControl
    {
        private string _content;
        public string Content
        {
            get => _content;
            set => SetAndRaise(ContentProperty, ref _content, value);
        }
        public static readonly DirectProperty<Text, string> ContentProperty =
            AvaloniaProperty.RegisterDirect<Text, string>(
                nameof(Content),
                o => o.Content,
                (o, v) => o.Content = v);

        private bool _value;
        public bool Value
        {
            get => _value;
            set => SetAndRaise(ValueProperty, ref _value, value);
        }
        public static readonly DirectProperty<Text, bool> ValueProperty =
            AvaloniaProperty.RegisterDirect<Text, bool>(
                nameof(Value),
                o => o.Value,
                (o, v) => o.Value = v);

        private string _textValue;
        public string TextValue
        {
            get => _textValue;
            set => SetAndRaise(TextValueProperty, ref _textValue, value);
        }
        public static readonly DirectProperty<Text, string> TextValueProperty =
            AvaloniaProperty.RegisterDirect<Text, string>(
                nameof(TextValue),
                o => o.TextValue,
                (o, v) => o.TextValue = v);

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if(change.Property == ValueProperty)
            {
                var c = change.Sender as Text;
                c.SetContent();
            }
        }

        protected override Type StyleKeyOverride => typeof(Text);


        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            SetContent();
        }

        private void SetContent()
        {
            if (string.IsNullOrEmpty(Content) || Content.IndexOf('?') == -1 || Content.IndexOf(':') == -1)
            {
                return;
            }

            string yes = Content.Substring(Content.IndexOf('?') + 1, Content.IndexOf(':') - 1);
            string no = Content.Substring(Content.IndexOf(':') + 1);
            TextValue = Value ? yes : no;
        }
    }
}

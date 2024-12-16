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
        public string Content
        {
            get { return GetValue(ContentProperty); }
            set { SetValue(ContentProperty, value); }
        }
        public static readonly StyledProperty<string> ContentProperty =
            AvaloniaProperty.Register<Text, string>(nameof(Content));  
        public bool Value
        {
            get { return GetValue(ValueProperty); }
            set { SetValue(ValueProperty, value); }
        }
        public static readonly StyledProperty<bool> ValueProperty =
               AvaloniaProperty.Register<Text, bool>(nameof(Value));

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

        public string TextValue
        {
            get { return GetValue(TextValueProperty); }
            set { SetValue(TextValueProperty, value); }
        }
        public static readonly StyledProperty<string> TextValueProperty =
            AvaloniaProperty.Register<Text, string>(nameof(TextValue));

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

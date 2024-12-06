using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UI.Controls.Toggle
{
    public class Toggle : TemplatedControl
    {
        public event EventHandler ToggleChanged;

        public bool IsChecked
        {
            get { return GetValue(IsCheckedProperty); }
            set { SetValue(IsCheckedProperty, value); }
        }

        public static readonly StyledProperty<bool> IsCheckedProperty =
            AvaloniaProperty.Register<Toggle, bool>(nameof(IsChecked));

        public ToggleTextPosition TextPosition
        {
            get { return GetValue(TextPositionProperty); }
            set { SetValue(TextPositionProperty, value); }
        }

        public static readonly StyledProperty<ToggleTextPosition> TextPositionProperty =
        AvaloniaProperty.Register<Toggle, ToggleTextPosition>(nameof(IsChecked), ToggleTextPosition.Right);

        public string OnText
        {
            get { return GetValue(OnTextProperty); }
            set { SetValue(OnTextProperty, value); }
        }

        public static readonly StyledProperty<string> OnTextProperty =
         AvaloniaProperty.Register<Toggle,string>(nameof(OnText), "On");

        public string OffText
        {
            get { return (string)GetValue(OffTextProperty); }
            set { SetValue(OffTextProperty, value); }
        }

        public static readonly StyledProperty<string> OffTextProperty =
          AvaloniaProperty.Register<Toggle,string>(nameof(OffText), "Off");

        public string Text
        {
            get { return (string)GetValue(TextProperty); }
            set { SetValue(TextProperty, value); }
        }
        public static readonly StyledProperty<string> TextProperty =
           AvaloniaProperty.Register<Toggle, string>(nameof(Text));

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            IsChecked = !IsChecked;
            ToggleChanged?.Invoke(this, EventArgs.Empty);
        }

        protected override Type StyleKeyOverride => typeof(Toggle);
    }
}

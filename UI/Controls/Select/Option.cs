using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UI.Controls.Select
{
    public class Option : TemplatedControl
    {
        public bool IsShowIcon
        {
            get { return (bool)GetValue(IsShowIconProperty); }
            set { SetValue(IsShowIconProperty, value); }
        }

        public static readonly StyledProperty<bool> IsShowIconProperty =
          AvaloniaProperty.Register<Option,bool>(nameof(IsShowIcon));

        public bool IsChecked
        {
            get { return (bool)GetValue(IsCheckedProperty); }
            set { SetValue(IsCheckedProperty, value); }
        }

        public static readonly StyledProperty<bool> IsCheckedProperty =
           AvaloniaProperty.Register<Option,bool>(nameof(IsChecked));

        public SelectItemModel Value
        {
            get { return (SelectItemModel)GetValue(ValueProperty); }
            set { SetValue(ValueProperty, value); }
        }

        public static readonly StyledProperty<SelectItemModel> ValueProperty =
          AvaloniaProperty.Register<Option,SelectItemModel>(nameof(Value));


        protected override Type StyleKeyOverride => typeof(Option);

        public Option()
        {
            this.PointerPressed += OnPointerPressed;
        }

        protected override void OnUnloaded(RoutedEventArgs e)
        {
            base.OnUnloaded(e);
            this.PointerPressed -= OnPointerPressed;
        }

        private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            IsChecked = !IsChecked;
        }
    }
}

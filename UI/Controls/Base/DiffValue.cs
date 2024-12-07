using Avalonia;
using Avalonia.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UI.Controls.Base
{
    public class DiffValue : StackPanel
    {
        public enum DiffType
        {
            Percent,
            Number
        }

        public double Value
        {
            get { return GetValue(ValueProperty); }
            set { SetValue(ValueProperty, value); }
        }

        public static readonly StyledProperty<double> ValueProperty =
          AvaloniaProperty.Register<DiffValue, double>(nameof(Value));

        public double LastValue
        {
            get { return GetValue(LastValueProperty); }
            set { SetValue(LastValueProperty, value); }
        }
        public static readonly StyledProperty<double> LastValueProperty =
             AvaloniaProperty.Register<DiffValue, double>(nameof(LastValue));

        public DiffType Type
        {
            get { return (DiffType)GetValue(TypeProperty); }
            set { SetValue(TypeProperty, value); }
        }
        public static readonly StyledProperty<DiffType> TypeProperty =
            AvaloniaProperty.Register<DiffValue, DiffType>(nameof(Type));

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == TypeProperty ||
                change.Property == LastValueProperty ||
                change.Property == ValueProperty)
            {
                var control = change.Sender as DiffValue;
                control.Render();
            }
        }

        public DiffValue()
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal;
            Render();
        }

        private void Render()
        {
            Children.Clear();

            double diffValue = Value - LastValue;
            double result = Type == DiffType.Percent ? diffValue / LastValue * 100 : diffValue;
            if (Value > 0 && LastValue <= 0)
            {
                result = Type == DiffType.Percent ? 100 : Value;
            }
            else if (Value == LastValue)
            {
                result = 0;
            }
            var text = new TextBlock();
            text.Text = Type == DiffType.Percent ? Math.Abs(result).ToString("f2") + "%" : Math.Abs(result).ToString();
            text.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;

            var icon = new Icon();
            icon.FontSize = 10;
            icon.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;

            if (result > 0)
            {
                icon.IconType = IconTypes.ArrowUp8;
                Children.Add(icon);
                Children.Add(text);
            }
            else if (result < 0)
            {
                icon.IconType = IconTypes.ArrowDown8;
                Children.Add(icon);
                Children.Add(text);
            }
            else
            {
                icon.IconType = IconTypes.SubtractBold;
                Children.Add(icon);
            }


        }

        protected override Type StyleKeyOverride => typeof(DiffValue);
    }
}

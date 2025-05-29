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

        // Value 属性
        private double _value;
        public double Value
        {
            get => _value;
            set => SetAndRaise(ValueProperty, ref _value, value);
        }
        public static readonly DirectProperty<DiffValue, double> ValueProperty =
            AvaloniaProperty.RegisterDirect<DiffValue, double>(
                nameof(Value),
                o => o.Value,
                (o, v) => o.Value = v);
        
        private double _lastValue;
        public double LastValue
        {
            get => _lastValue;
            set => SetAndRaise(LastValueProperty, ref _lastValue, value);
        }
        public static readonly DirectProperty<DiffValue, double> LastValueProperty =
            AvaloniaProperty.RegisterDirect<DiffValue, double>(
                nameof(LastValue),
                o => o.LastValue,
                (o, v) => o.LastValue = v);
        
        private DiffType _type;
        public DiffType Type
        {
            get => _type;
            set => SetAndRaise(TypeProperty, ref _type, value);
        }
        public static readonly DirectProperty<DiffValue, DiffType> TypeProperty =
            AvaloniaProperty.RegisterDirect<DiffValue, DiffType>(
                nameof(Type),
                o => o.Type,
                (o, v) => o.Type = v);

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

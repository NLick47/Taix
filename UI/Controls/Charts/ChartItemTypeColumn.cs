using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UI.Base.Color;

namespace UI.Controls.Charts
{
    public class ChartItemTypeColumn : TemplatedControl
    {
        public double MaxValue
        {
            get { return (double)GetValue(MaxValueProperty); }
            set { SetValue(MaxValueProperty, value); }
        }
        public static readonly StyledProperty<double> MaxValueProperty =
            AvaloniaProperty.Register<ChartItemTypeColumn,double>(nameof(MaxValue));

        public double Value
        {
            get { return (double)GetValue(ValueProperty); }
            set { SetValue(ValueProperty, value); }
        }
        public static readonly StyledProperty<double> ValueProperty =
             AvaloniaProperty.Register<ChartItemTypeColumn, double>(nameof(Value));

        public string Color
        {
            get { return (string)GetValue(ColorProperty); }
            set { SetValue(ColorProperty, value); }
        }
        public static readonly StyledProperty<string> ColorProperty =
            AvaloniaProperty.Register<ChartItemTypeColumn,string>(nameof(Color));

        public string ColumnName
        {
            get { return (string)GetValue(ColumnNameProperty); }
            set { SetValue(ColumnNameProperty, value); }
        }
        public static readonly StyledProperty<string> ColumnNameProperty =
            AvaloniaProperty.Register<ChartItemTypeColumn,string>(nameof(ColumnName));


        private bool isRendering = false;
        private Rectangle ValueBlockObj;
        private Border ValueContainer;
        private bool IsAddEvent = false;

        protected override Type StyleKeyOverride => typeof(ChartItemTypeColumn);

        public ChartItemTypeColumn()
        {
            Unloaded += ChartItemTypeColumn_Unloaded;
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            ValueBlockObj = e.NameScope.Find("ValueBlockObj") as Rectangle;
            ValueContainer = e.NameScope.Find("ValueContainer") as Border;
            if (!IsAddEvent)
            {
                Loaded += ChartItemTypeColumn_Loaded;
            }
        }

        private void ChartItemTypeColumn_Unloaded(object sender, RoutedEventArgs e)
        {
            Loaded -= ChartItemTypeColumn_Loaded;
            Unloaded -= ChartItemTypeColumn_Unloaded;
        }

        private void ChartItemTypeColumn_Loaded(object sender, RoutedEventArgs e)
        {
            Render();
            IsAddEvent = true;
        }

        private void Render()
        {
            if (isRendering)
            {
                return;
            }

            if (!string.IsNullOrEmpty(Color))
            {
                ValueBlockObj.Fill = Colors.GetFromString(Color);
            }
            Update();
        }
        public void Update()
        {
            ValueBlockObj.Height = (Value / MaxValue) * (ValueContainer.Bounds.Height);
        }
    }
}

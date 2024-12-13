using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.Media;
using Core.Librarys;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UI.Controls.Charts.Model;

namespace UI.Controls.Charts
{
    public class ChartsItemTypeRadar : TemplatedControl
    {
        public List<ChartsDataModel> Data
        {
            get { return GetValue(DataProperty); }
            set { SetValue(DataProperty, value); }
        }
        public static readonly StyledProperty<List<ChartsDataModel>> DataProperty =
            AvaloniaProperty.Register<ChartsItemTypeRadar, List<ChartsDataModel>>(nameof(Data));

        public double MaxValue
        {
            get { return GetValue(MaxValueProperty); }
            set { SetValue(MaxValueProperty, value); }
        }
        public static readonly StyledProperty<double> MaxValueProperty =
            AvaloniaProperty.Register<ChartsItemTypeRadar, double>(nameof(MaxValue));

        public bool IsLoading
        {
            get { return GetValue(IsLoadingProperty); }
            set { SetValue(IsLoadingProperty, value); }
        }
        public static readonly StyledProperty<bool> IsLoadingProperty =
           AvaloniaProperty.Register<ChartsItemTypeRadar, bool>(nameof(IsLoading));

        public Geometry RadarPathData
        {
            get { return GetValue(RadarPathDataProperty); }
            set { SetValue(RadarPathDataProperty, value); }
        }
        public static readonly StyledProperty<Geometry> RadarPathDataProperty =
           AvaloniaProperty.Register<ChartsItemTypeRadar, Geometry>(nameof(RadarPathData));

        private Canvas canvas;

        protected override Type StyleKeyOverride => typeof(ChartsItemTypeRadar);

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            canvas = e.NameScope.Get<Canvas>("Canvas");
            Loaded += ChartsItemTypeRadar_Loaded;
        }

        private void ChartsItemTypeRadar_Loaded(object sender, RoutedEventArgs e)
        {
            Render();
        }

        private void Render()
        {
            if (Data == null || Data.Count == 0)
            {
                return;
            }
            canvas.Children.Clear();
            double size = Bounds.Width != double.NaN ? Bounds.Width : 200;
            //size -= 50;
            int count = Data.Count;

            //  边长
            double lineWidth = size / 2;

            //  角度
            double angle = (Math.PI * 2) / count;

            var r = lineWidth / count;

            //多边形边框
            for (int i = 0; i < 3; i++)
            {
                var points = new List<Point>();
                //  当前半径
                var currR = (r - (5 * i)) * (count - i);

                for (int j = 0; j < count; j++)
                {
                    points.Add(new Point(lineWidth + currR * Math.Cos(angle * j), lineWidth + currR * Math.Sin(angle * j)));
                }
                //  画出边框
                for (int i2 = 0; i2 < points.Count; i2++)
                {
                    var line = new Line();

                    line.StartPoint = points[i2];

                    line.EndPoint = i2 == points.Count - 1 ? points[0] : points[i2 + 1];

                    line.StrokeThickness = i == 0 ? 1 : 1;

                    line.Stroke = UI.Base.Color.Colors.GetFromString(i == 0 ? "#eeeef2" : "#eeeef2");

                    canvas.Children.Add(line);
                }

            }

            //  顶点连线
            for (var i = 0; i < count; i++)
            {
                //double diff = i == 0 || i == count - 1 ? 0 : 5;
                double diff = 0;
                var x = (double)(lineWidth + (lineWidth - diff) * Math.Cos((angle) * i));
                var y = (double)(lineWidth + (lineWidth - diff) * Math.Sin((angle) * i));

                var line = new Line();
                line.StartPoint = new Point(lineWidth, lineWidth);
                line.EndPoint = new Point(x, y);

                line.StrokeThickness = .5;
                line.Stroke = UI.Base.Color.Colors.GetFromString("#dedede");

                canvas.Children.Add(line);

                //  类别文字
                var font = new TextBlock();
                font.Text = Data[i].Name.Length > 4 ? Data[i].Name.Substring(0, 4) : Data[i].Name;
                font.Foreground = UI.Base.Color.Colors.GetFromString("#7f7f7f");
                font.FontSize = 12;
                ToolTip.SetTip(font, $"{Data[i].Name} {Time.ToString((int)Data[i].Values.Sum())}");

                var textSize = MeasureString(font);
                Debug.WriteLine(font.Text + " -> " + angle * i);
                if (angle * i > 0 && angle * i <= Math.PI / 2)
                {
                    // >0 && <  1.57
                    x += textSize.Height / 2;
                    y += textSize.Height / 2;
                }
                else if (angle * i > 1.79 && angle * i < 1.8)
                {
                    x += textSize.Height / 2;
                    y += textSize.Width / 2;
                }
                else if (angle * i > 2.09 && angle * i < 2.1)
                {
                    x += textSize.Height / 2;
                    y += textSize.Height / 2;
                }
                else if (angle * i > 2.51 && angle * i < 2.52)
                {
                    x += textSize.Height / 2;
                    y += textSize.Height / 2;
                }
                else if (angle * i > 2.69 && angle * i < 2.7)
                {
                    x += textSize.Height / 2;
                    y += textSize.Height / 2;
                }
                else if (angle * i > Math.PI / 2 && angle * i <= Math.PI)
                {
                    // > 1.57 && < 3.14
                    x -= textSize.Height / 2;
                    y -= textSize.Width / 2;

                }
                //else if (angle * i > 3.5 && angle * i < 4.2)
                //{
                //    x -= textSize.Height / 2;
                //    y -= textSize.Width / 2;

                //}
                else if (angle * i > Math.PI && angle * i <= Math.PI * 3 / 2)
                {
                    // > 3.14 && < 4.71
                    x += font.FontSize / 2;
                    y -= textSize.Width + textSize.Height / 2;
                }
                else if (angle * i > Math.PI * 3 / 2)
                {
                    //  > 4.71
                    x += textSize.Height / 2;
                    y -= textSize.Width + textSize.Height / 2;
                }
                else
                {
                    //  顶点
                    x += textSize.Height + textSize.Height / 2;
                    y -= textSize.Width / 2;
                }

                font.RenderTransform = new RotateTransform()
                {
                    Angle = 90
                };
                Canvas.SetLeft(font, x);
                Canvas.SetTop(font, y);
                canvas.Children.Add(font);
            }

            //  中心装饰点
            var centerPoint = new Ellipse();
            centerPoint.Width = 15;
            centerPoint.Height = 15;
            centerPoint.Fill = UI.Base.Color.Colors.GetFromString("#ffffff");
            centerPoint.Stroke = UI.Base.Color.Colors.GetFromString("#dedede");
            centerPoint.StrokeThickness = 1;
            Canvas.SetLeft(centerPoint, lineWidth - centerPoint.Width / 2);
            Canvas.SetTop(centerPoint, lineWidth - centerPoint.Width / 2);
            canvas.Children.Add(centerPoint);

            //  数据区域
            var pc = new List<Point>();

            for (int i = 0; i < count; i++)
            {
                double sum = Data[i].Values.Sum();
                double value = sum / MaxValue;
                value = value == 1 ? 0.97 : value;

                var x = (double)(lineWidth + (lineWidth) * Math.Cos((angle) * i) * value);
                var y = (double)(lineWidth + (lineWidth) * Math.Sin((angle) * i) * value);

                pc.Add(new Point(x, y));

                //  数据点
                var dataPoint = new Ellipse();
                dataPoint.Width = 5;
                dataPoint.Height = 5;

                dataPoint.Fill = UI.Base.Color.Colors.GetFromString(StateData.ThemeColor);
                dataPoint.Stroke = UI.Base.Color.Colors.GetFromString(StateData.ThemeColor);
                dataPoint.StrokeThickness = 1;
                Canvas.SetLeft(dataPoint, x - dataPoint.Width / 2);
                Canvas.SetTop(dataPoint, y - dataPoint.Width / 2);
                canvas.Children.Add(dataPoint);
            }

            var p = new Polygon();
            p.Stroke = UI.Base.Color.Colors.GetFromString(StateData.ThemeColor);
            p.Fill = UI.Base.Color.Colors.GetFromString(StateData.ThemeColor, .3);
            p.StrokeThickness = 1;
            p.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
            p.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
            p.Points = pc;
            canvas.Children.Add(p);
        }

        private Size MeasureString(TextBlock textBlock)
        {
            var formattedText = new FormattedText(
                textBlock.Text,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(textBlock.FontFamily, textBlock.FontStyle, textBlock.FontWeight, textBlock.FontStretch),
                textBlock.FontSize,
                Brushes.Black);

            return new Size(formattedText.Width, formattedText.Height);
        }
    }
}

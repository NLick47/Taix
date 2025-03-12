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
using Avalonia.Input;
using UI.Controls.Charts.Model;

namespace UI.Controls.Charts
{
    public class RadarChart : Control
    {
        public static readonly StyledProperty<double> MaxValueProperty =
            AvaloniaProperty.Register<RadarChart, double>(nameof(MaxValue), 100);

        public static readonly StyledProperty<List<double>> ValuesProperty =
            AvaloniaProperty.Register<RadarChart, List<double>>(nameof(Values));

        public static readonly StyledProperty<List<string>> LabelsProperty =
            AvaloniaProperty.Register<RadarChart, List<string>>(nameof(Labels));
        
        public double MaxValue
        {
            get => GetValue(MaxValueProperty);
            set => SetValue(MaxValueProperty, value);
        }

        public List<double> Values
        {
            get => GetValue(ValuesProperty);
            set => SetValue(ValuesProperty, value);
        }

        public List<string> Labels
        {
            get => GetValue(LabelsProperty);
            set => SetValue(LabelsProperty, value);
        }


        public override void Render(DrawingContext context)
        {
            base.Render(context);

            if (Values == null || Values.Count < 3) return;

            var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
            var radius = Math.Min(Bounds.Width, Bounds.Height) / 2 * 0.7;

            DrawGrid(context, center, radius);

            DrawDataPolygon(context, center, radius);

            DrawLabels(context, center, radius);

            DrawCenterAndVertexPoints(context, center, radius);
        }
        
        private void DrawLabels(DrawingContext context, Point center, double radius)
        {
            if (Labels == null || Labels.Count != Values.Count) return;

            var typeface = new Typeface("Microsoft YaHei");
            var brush = UI.Base.Color.Colors.GetFromString("#7f7f7f");
            ;

            for (int i = 0; i < Labels.Count; i++)
            {
                // 计算标签的位置
                var angle = 2 * Math.PI * i / Values.Count - Math.PI / 2;
                var labelPoint = new Point(
                    center.X + (radius + 25) * Math.Cos(angle),
                    center.Y + (radius + 20) * Math.Sin(angle));

                // 创建格式化文本
                var formattedText = new FormattedText(
                    Labels[i],
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    typeface,
                    12,
                    brush);
                // 调整标签位置，使其居中
                labelPoint = new Point(
                    labelPoint.X - formattedText.Width / 2,
                    labelPoint.Y - formattedText.Height / 2);
                // 绘制标签
                context.DrawText(formattedText, labelPoint);
            }
        }

        private List<Point> CalculatePolygonPoints(Point center, double radius, int sides)
        {
            var points = new List<Point>();
            for (int i = 0; i < sides; i++)
            {
                // 计算每个顶点的角度
                var angle = 2 * Math.PI * i / sides - Math.PI / 2; // 从顶部开始
                // 计算顶点的坐标
                var point = new Point(
                    center.X + radius * Math.Cos(angle),
                    center.Y + radius * Math.Sin(angle));
                points.Add(point);
            }

            return points;
        }

        private void DrawGrid(DrawingContext context, Point center, double radius)
        {
            var pen = new Pen(UI.Base.Color.Colors.GetFromString("#eeeef1"),0.8);

            // 绘制同心多边形
            for (int i = 1; i <= 3; i++)
            {
                var points = CalculatePolygonPoints(center, radius * i / 3, Values.Count);
                context.DrawGeometry(null, pen, new PolylineGeometry(points, true));
            }

            // 绘制轴线
            for (int i = 0; i < Values.Count; i++)
            {
                var angle = 2 * Math.PI * i / Values.Count - Math.PI / 2;
                var endPoint = new Point(
                    center.X + radius * Math.Cos(angle),
                    center.Y + radius * Math.Sin(angle));
                context.DrawLine(pen, center, endPoint);
            }
        }

        private void DrawDataPolygon(DrawingContext context, Point center, double radius)
        {
            var points = new List<Point>();
            for (int i = 0; i < Values.Count; i++)
            {
                var value = Values[i] / MaxValue;
                value = value == 1 ? 0.97 : value;
                var r = radius * value;
                var angle = 2 * Math.PI * i / Values.Count - Math.PI / 2;

                points.Add(new Point(
                    center.X + r * Math.Cos(angle),
                    center.Y + r * Math.Sin(angle)));
            }

            // 绘制填充区域
            context.DrawGeometry(
                new SolidColorBrush(Color.Parse(StateData.ThemeColor), 0.3),
                new Pen(new SolidColorBrush(Color.Parse(StateData.ThemeColor)), 2),
                new PolylineGeometry(points, true));
        }
        
        private void DrawCenterAndVertexPoints(DrawingContext context, Point center, double radius)
        {
            // 创建画刷和笔刷
            var centerBrush = UI.Base.Color.Colors.GetFromString(StateData.ThemeColor);
            var borderBrush = new Pen( UI.Base.Color.Colors.GetFromString("#dedede")); 

            context.DrawEllipse(
                UI.Base.Color.Colors.GetFromString("#ffffff"),
                borderBrush,
                center,
                7,
                7
            );

            // 绘制顶点圆形
            var points = new List<Point>();
            for (int i = 0; i < Values.Count; i++)
            {
                var value = Values[i] / MaxValue;
                value = value == 1 ? 0.97 : value;
                var r = radius * value;
                var angle = 2 * Math.PI * i / Values.Count - Math.PI / 2;

                var point = new Point(
                    center.X + r * Math.Cos(angle),
                    center.Y + r * Math.Sin(angle));

                points.Add(point);
                context.DrawEllipse(centerBrush, null, point, 3, 3);
            }
        }


        private Size MeasureRotatedString(TextBlock textBlock)
        {
            var formattedText = new FormattedText(
                textBlock.Text,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(textBlock.FontFamily, textBlock.FontStyle, textBlock.FontWeight, textBlock.FontStretch),
                textBlock.FontSize,
                Brushes.Black);
            return new Size(formattedText.Height, formattedText.Width);
        }
    }
}
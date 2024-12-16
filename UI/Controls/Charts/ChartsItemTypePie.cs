using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UI.Controls.Charts.Model;

namespace UI.Controls.Charts
{
    public class ChartsItemTypePie : Canvas
    {
        /// <summary>
        /// Data
        /// </summary>
        public List<ChartsDataModel> Data
        {
            get { return GetValue(DataProperty); }
            set { SetValue(DataProperty, value); }
        }
        public static readonly StyledProperty<List<ChartsDataModel>> DataProperty =
            AvaloniaProperty.Register<ChartsItemTypePie, List<ChartsDataModel>>(nameof(Data));


        /// <summary>
        /// 最大值
        /// </summary>
        public double MaxValue
        {
            get { return GetValue(MaxValueProperty); }
            set { SetValue(MaxValueProperty, value); }
        }
        public static readonly StyledProperty<double> MaxValueProperty =
            AvaloniaProperty.Register<ChartsItemTypePie,double>(nameof(MaxValue));

        private double _lastAngle = -Math.PI / 2;
        private int _zIndex = 1;
        private List<Path> _paths = new List<Path>();

        protected override Type StyleKeyOverride => typeof(ChartsItemTypePie);

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);
            Render();
        }

        protected override void OnUnloaded(RoutedEventArgs e)
        {
            base.OnUnloaded(e);
            foreach(var item in _paths)
            {
                item.PointerEntered -= Path_PointerEntered;
                item.PointerExited -= Path_PointerExited;
            }
            foreach (var item in Children)
            {
                item.PointerEntered -= Path_PointerEntered;
                item.PointerExited -= Path_PointerExited;
            }
        }

        private void Render()
        {
            _paths.Clear();
            Children.Clear();
            MaxValue = Data.Sum(m => m.Value);

            int i = 0;
            foreach (var item in Data)
            {
                var angle = item.Value / MaxValue * 360;
                var path = CreatePath(angle, UI.Base.Color.Colors.GetFromString(item.Color));
                //path.ToolTip = item.PopupText;
                path.PointerEntered += Path_PointerEntered;
                path.PointerExited += Path_PointerExited;
                _paths.Add(path);
                Children.Add(path);
                i++;
            }
        }

        private void Path_PointerExited(object? sender, PointerEventArgs e)
        {
            foreach (var p in _paths)
            {
                p.Opacity = 1;
            }
        }

        private void Path_PointerEntered(object? sender, PointerEventArgs e)
        {
            var path = sender as Path;
            foreach (var p in _paths)
            {
                if (p != path)
                {
                    p.Opacity = .2;
                }
            }
        }

        private Path CreatePath(double angle_, SolidColorBrush color_)
        {
            Path path = new Path();

            PathGeometry pathGeometry = new PathGeometry();
            double Radius = Bounds.Height / 2;
            //double Angle = angle_;
            //Point startPoint = new Point(Radius, Radius);

            //if (Angle >= 360)
            //{
            //    Angle = 359;
            //}


            double x = Math.Cos(_lastAngle) * Radius + Radius;
            double y = Math.Sin(_lastAngle) * Radius + Radius;
            var lin1 = new LineSegment() { Point = new Point(x, y) };

            _lastAngle += Math.PI * angle_ / 180;

            x = Math.Cos(_lastAngle) * Radius + Radius;
            y = Math.Sin(_lastAngle) * Radius + Radius;
            //Point endPoint = ComputeCartesianCoordinate(Angle, Radius);
            //endPoint.X += Radius;
            //endPoint.Y += Radius;

            //_lastX = endPoint.X;
            //_lastY = endPoint.Y;
            //Debug.WriteLine($"angle:{angle_},start:{startPoint},endpoint:{endPoint}");

            var arcSeg = new ArcSegment()
            {
                Size = new Size(Radius, Radius),
                IsLargeArc = angle_ > 180,
                SweepDirection = SweepDirection.Clockwise,
                Point = new Point(x, y),
                RotationAngle = angle_,
            };
            var line2 = new LineSegment() { Point = new Point(Radius, Radius) };
            var fig = new PathFigure()
            {
                 StartPoint = new Point(Radius, Radius),
                 Segments = new PathSegments { lin1, arcSeg, line2 }, 
                 IsClosed = false
            };
            pathGeometry.Figures.Add(fig);
            path.Data = pathGeometry;
            path.Fill = color_;
            return path;
        }

        private Point ComputeCartesianCoordinate(double angle, double radius)
        {
            // convert to radians
            double angleRad = (Math.PI / 180.0) * (angle - 90);

            double x = radius * Math.Cos(angleRad);
            double y = radius * Math.Sin(angleRad);

            return new Point(x, y);
        }


    }
}

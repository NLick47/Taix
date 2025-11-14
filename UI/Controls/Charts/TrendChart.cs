using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using UI.Controls.Charts.Model;

namespace UI.Controls.Charts
{
    public class TrendChart : Control
    {
        // 依赖属性
        public static readonly StyledProperty<IList<TrendDataPoint>> DataPointsProperty =
            AvaloniaProperty.Register<TrendChart, IList<TrendDataPoint>>(nameof(DataPoints));

        public static readonly StyledProperty<string> TitleProperty =
            AvaloniaProperty.Register<TrendChart, string>(nameof(Title), "趋势图");

        public static readonly StyledProperty<IBrush> LineBrushProperty =
            AvaloniaProperty.Register<TrendChart, IBrush>(nameof(LineBrush), Brushes.DodgerBlue);

        public static readonly StyledProperty<IBrush> FillBrushProperty =
            AvaloniaProperty.Register<TrendChart, IBrush>(nameof(FillBrush), 
                new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                    GradientStops = new GradientStops
                    {
                        new GradientStop(Color.FromArgb(76, 129, 140, 248), 0.05),
                        new GradientStop(Color.FromArgb(0, 129, 140, 248), 0.95)
                    }
                });

        public static readonly StyledProperty<IBrush> GridBrushProperty =
            AvaloniaProperty.Register<TrendChart, IBrush>(nameof(GridBrush), Brushes.Gray);

        public static readonly StyledProperty<IBrush> AxisBrushProperty =
            AvaloniaProperty.Register<TrendChart, IBrush>(nameof(AxisBrush), Brushes.Gray);

        public static readonly StyledProperty<IBrush> TextBrushProperty =
            AvaloniaProperty.Register<TrendChart, IBrush>(nameof(TextBrush), Brushes.LightGray);

        public static readonly StyledProperty<IBrush> BorderBrushProperty =
            AvaloniaProperty.Register<TrendChart, IBrush>(nameof(BorderBrush), Brushes.Gray);

        public static readonly StyledProperty<double> BorderThicknessProperty =
            AvaloniaProperty.Register<TrendChart, double>(nameof(BorderThickness), 1.0);

        public static readonly StyledProperty<FontFamily> FontFamilyProperty =
            AvaloniaProperty.Register<TrendChart, FontFamily>(nameof(FontFamily), FontFamily.Default);

        public static readonly StyledProperty<double> LineThicknessProperty =
            AvaloniaProperty.Register<TrendChart, double>(nameof(LineThickness), 2.0);

        public static readonly StyledProperty<double> PointSizeProperty =
            AvaloniaProperty.Register<TrendChart, double>(nameof(PointSize), 6.0);

        public static readonly StyledProperty<double> HoverPointSizeProperty =
            AvaloniaProperty.Register<TrendChart, double>(nameof(HoverPointSize), 8.0);

        public static readonly StyledProperty<double> MarginProperty =
            AvaloniaProperty.Register<TrendChart, double>(nameof(Margin), 40.0);

        public static readonly StyledProperty<bool> ShowGridProperty =
            AvaloniaProperty.Register<TrendChart, bool>(nameof(ShowGrid), true);

        public static readonly StyledProperty<bool> ShowBorderProperty =
            AvaloniaProperty.Register<TrendChart, bool>(nameof(ShowBorder), true);

        public static readonly StyledProperty<int> GridLinesCountProperty =
            AvaloniaProperty.Register<TrendChart, int>(nameof(GridLinesCount), 10);

        public static readonly StyledProperty<int> YAxisLabelIntervalProperty =
            AvaloniaProperty.Register<TrendChart, int>(nameof(YAxisLabelInterval), 1);

        public static readonly StyledProperty<int> XAxisLabelIntervalProperty =
            AvaloniaProperty.Register<TrendChart, int>(nameof(XAxisLabelInterval), 1);

        public static readonly StyledProperty<bool> ShowTooltipProperty =
            AvaloniaProperty.Register<TrendChart, bool>(nameof(ShowTooltip), true);

        // 私有字段
        private Point _mousePosition = new Point(double.NaN, double.NaN);
        private int _hoveredIndex = -1;
        private bool _isMouseOver = false;
        private readonly DispatcherTimer _animationTimer;
        private readonly List<Point> _animatedPoints = new List<Point>();
        private readonly List<Point> _targetPoints = new List<Point>();
        private double _animationProgress = 1.0;
        private Rect _chartRect;
        private bool _needsAnimationReset = true;

        // 构造函数
        public TrendChart()
        {
            // 动画定时器
            _animationTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            _animationTimer.Tick += AnimationTimer_Tick;
            
            // 初始化动画点
            InitializeAnimationPoints();
            
            // 布局变化时重置动画
            this.GetObservable(BoundsProperty).Subscribe(_ => 
            {
                _needsAnimationReset = true;
                InvalidateVisual();
            });
        }

        // 属性
        public IList<TrendDataPoint> DataPoints
        {
            get => GetValue(DataPointsProperty);
            set => SetValue(DataPointsProperty, value);
        }

        public string Title
        {
            get => GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public IBrush LineBrush
        {
            get => GetValue(LineBrushProperty);
            set => SetValue(LineBrushProperty, value);
        }

        public IBrush FillBrush
        {
            get => GetValue(FillBrushProperty);
            set => SetValue(FillBrushProperty, value);
        }

        public IBrush GridBrush
        {
            get => GetValue(GridBrushProperty);
            set => SetValue(GridBrushProperty, value);
        }

        public IBrush AxisBrush
        {
            get => GetValue(AxisBrushProperty);
            set => SetValue(AxisBrushProperty, value);
        }

        public IBrush TextBrush
        {
            get => GetValue(TextBrushProperty);
            set => SetValue(TextBrushProperty, value);
        }

        public IBrush BorderBrush
        {
            get => GetValue(BorderBrushProperty);
            set => SetValue(BorderBrushProperty, value);
        }

        public double BorderThickness
        {
            get => GetValue(BorderThicknessProperty);
            set => SetValue(BorderThicknessProperty, value);
        }

        public FontFamily FontFamily
        {
            get => GetValue(FontFamilyProperty);
            set => SetValue(FontFamilyProperty, value);
        }

        public double LineThickness
        {
            get => GetValue(LineThicknessProperty);
            set => SetValue(LineThicknessProperty, value);
        }

        public double PointSize
        {
            get => GetValue(PointSizeProperty);
            set => SetValue(PointSizeProperty, value);
        }

        public double HoverPointSize
        {
            get => GetValue(HoverPointSizeProperty);
            set => SetValue(HoverPointSizeProperty, value);
        }

        public double Margin
        {
            get => GetValue(MarginProperty);
            set => SetValue(MarginProperty, value);
        }

        public bool ShowGrid
        {
            get => GetValue(ShowGridProperty);
            set => SetValue(ShowGridProperty, value);
        }

        public bool ShowBorder
        {
            get => GetValue(ShowBorderProperty);
            set => SetValue(ShowBorderProperty, value);
        }

        public int GridLinesCount
        {
            get => GetValue(GridLinesCountProperty);
            set => SetValue(GridLinesCountProperty, value);
        }

        public int YAxisLabelInterval
        {
            get => GetValue(YAxisLabelIntervalProperty);
            set => SetValue(YAxisLabelIntervalProperty, value);
        }

        public int XAxisLabelInterval
        {
            get => GetValue(XAxisLabelIntervalProperty);
            set => SetValue(XAxisLabelIntervalProperty, value);
        }

        public bool ShowTooltip
        {
            get => GetValue(ShowTooltipProperty);
            set => SetValue(ShowTooltipProperty, value);
        }
        
        private void InitializeAnimationPoints()
        {
            _animatedPoints.Clear();
            _targetPoints.Clear();
            
            if (DataPoints == null || DataPoints.Count == 0)
                return;
                
            foreach (var point in DataPoints)
            {
                _animatedPoints.Add(new Point(0, 0));
                _targetPoints.Add(new Point(0, 0));
            }
            
            _animationProgress = 0;
            _animationTimer.Start();
        }
        
        public override void Render(DrawingContext context)
        {
            base.Render(context);
            
            if (DataPoints == null || DataPoints.Count == 0)
                return;
            
            var size = Bounds.Size;
            _chartRect = new Rect(Margin, Margin, size.Width - Margin * 2, size.Height - Margin * 2);
            
            // 如果需要重置动画（布局变化）
            if (_needsAnimationReset && _chartRect.Width > 0 && _chartRect.Height > 0)
            {
                InitializeAnimationPoints();
                _needsAnimationReset = false;
            }
            
            // 绘制边框
            if (ShowBorder)
            {
                DrawBorder(context, _chartRect);
            }
            
            // 绘制网格
            if (ShowGrid)
            {
                DrawGrid(context, _chartRect);
            }
            
            // 绘制坐标轴
            DrawAxes(context, _chartRect);
            
            // 绘制数据
            DrawData(context, _chartRect);
            
            // 绘制悬停提示
            if (ShowTooltip && _isMouseOver && _hoveredIndex >= 0 && _hoveredIndex < DataPoints.Count)
            {
                DrawTooltip(context, _chartRect);
            }
        }
        
        private void DrawBorder(DrawingContext context, Rect chartRect)
        {
            var borderPen = new Pen(BorderBrush, BorderThickness);
            context.DrawRectangle(null, borderPen, chartRect);
        }
        
        private void DrawGrid(DrawingContext context, Rect chartRect)
        {
            var gridPen = new Pen(GridBrush, 1, new DashStyle(new double[] { 2, 2 }, 0));
            
            // 水平网格线
            for (int i = 0; i <= GridLinesCount; i++)
            {
                var y = chartRect.Top + (chartRect.Height / GridLinesCount) * i;
                context.DrawLine(gridPen, new Point(chartRect.Left, y), new Point(chartRect.Right, y));
            }
            
            // 垂直网格线
            for (int i = 0; i < DataPoints.Count; i++)
            {
                var x = chartRect.Left + (chartRect.Width / (DataPoints.Count - 1)) * i;
                context.DrawLine(gridPen, new Point(x, chartRect.Top), new Point(x, chartRect.Bottom));
            }
        }
        
        private string ConvertSecondsToTimeString(double seconds)
        {
            int totalSeconds = (int)seconds;
            
            if (totalSeconds < 60)
            {
                return $"{totalSeconds}秒";
            }
            else if (totalSeconds < 3600)
            {
                int minutes = totalSeconds / 60;
                int remainingSeconds = totalSeconds % 60;
                return remainingSeconds > 0 ? $"{minutes}分{remainingSeconds}秒" : $"{minutes}分";
            }
            else
            {
                int hours = totalSeconds / 3600;
                int remainingMinutes = (totalSeconds % 3600) / 60;
                int remainingSeconds = totalSeconds % 60;
                
                if (remainingMinutes > 0 && remainingSeconds > 0)
                    return $"{hours}时{remainingMinutes}分{remainingSeconds}秒";
                else if (remainingMinutes > 0)
                    return $"{hours}时{remainingMinutes}分";
                else if (remainingSeconds > 0)
                    return $"{hours}时{remainingSeconds}秒";
                else
                    return $"{hours}时";
            }
        }
        
        private string ConvertSecondsToYAxisLabel(double seconds)
        {
            int totalSeconds = (int)seconds;
            
            if (totalSeconds < 60)
            {
                return $"{totalSeconds}秒";
            }
            else if (totalSeconds < 3600)
            {
                return $"{totalSeconds / 60}分";
            }
            else
            {
                int hours = totalSeconds / 3600;
                int minutes = (totalSeconds % 3600) / 60;
                
                // 如果分钟数为0，只显示小时
                if (minutes == 0)
                    return $"{hours}时";
                // 如果小时数较大，只显示小时
                else if (hours >= 5)
                    return $"{hours}时";
                else
                    return $"{hours}时{minutes}分";
            }
        }
        
        private void DrawAxes(DrawingContext context, Rect chartRect)
        {
            var axisPen = new Pen(AxisBrush, 1);
            var textBrush = TextBrush;
            var fontSize = 12.0;
            
            // Y轴标签 - 使用间隔控制密度
            double maxValue = DataPoints.Max(p => p.Value);
            for (int i = 0; i <= GridLinesCount; i++)
            {
                // 使用间隔控制，只绘制指定间隔的标签
                if (i % YAxisLabelInterval != 0 && i != 0 && i != GridLinesCount)
                    continue;
                    
                var y = chartRect.Top + (chartRect.Height / GridLinesCount) * i;
                var value = maxValue - (maxValue / GridLinesCount) * i;
                
                // 使用简化的Y轴标签格式
                var labelText = ConvertSecondsToYAxisLabel(value);
                var text = new FormattedText(
                    labelText,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface(FontFamily, FontStyle.Normal, FontWeight.Normal, FontStretch.Normal),
                    fontSize,
                    textBrush
                );
                context.DrawText(text, new Point(chartRect.Left - 35, y - 6));
            }
            
            // X轴标签 - 使用间隔控制密度
            for (int i = 0; i < DataPoints.Count; i++)
            {
                // 使用间隔控制，只绘制指定间隔的标签
                if (i % XAxisLabelInterval != 0 && i != 0 && i != DataPoints.Count - 1)
                    continue;
                    
                var x = chartRect.Left + (chartRect.Width / (DataPoints.Count - 1)) * i;
                var text = new FormattedText(
                    DataPoints[i].Label,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface(FontFamily, FontStyle.Normal, FontWeight.Normal, FontStretch.Normal),
                    fontSize,
                    textBrush
                );
                context.DrawText(text, new Point(x - 10, chartRect.Bottom + 5));
            }
            
            // 绘制坐标轴线
            context.DrawLine(axisPen, new Point(chartRect.Left, chartRect.Top), new Point(chartRect.Left, chartRect.Bottom));
            context.DrawLine(axisPen, new Point(chartRect.Left, chartRect.Bottom), new Point(chartRect.Right, chartRect.Bottom));
        }
        
        private void DrawData(DrawingContext context, Rect chartRect)
        {
            if (DataPoints.Count < 2 || chartRect.Width <= 0 || chartRect.Height <= 0)
                return;
            
            // 计算目标点位置
            double maxX = DataPoints.Count - 1;
            double maxY = DataPoints.Max(p => p.Value);
            
            for (int i = 0; i < DataPoints.Count; i++)
            {
                var x = chartRect.Left + (i / maxX) * chartRect.Width;
                var y = chartRect.Bottom - (DataPoints[i].Value / maxY) * chartRect.Height;
                _targetPoints[i] = new Point(x, y);
            }
            
            // 更新动画点
            if (_animationProgress < 1.0)
            {
                for (int i = 0; i < DataPoints.Count; i++)
                {
                    var target = _targetPoints[i];
                    _animatedPoints[i] = new Point(
                        target.X,
                        chartRect.Bottom - (_animationProgress * (chartRect.Bottom - target.Y))
                    );
                }
            }
            else
            {
                for (int i = 0; i < DataPoints.Count; i++)
                {
                    _animatedPoints[i] = _targetPoints[i];
                }
            }
            
            // 绘制填充区域
            var pathGeometry = new StreamGeometry();
            using (var ctx = pathGeometry.Open())
            {
                // 开始于第一个点
                ctx.BeginFigure(_animatedPoints[0], true);
                
                // 连接所有点
                for (int i = 1; i < _animatedPoints.Count; i++)
                {
                    ctx.LineTo(_animatedPoints[i]);
                }
                
                // 闭合路径到底部
                ctx.LineTo(new Point(_animatedPoints.Last().X, chartRect.Bottom));
                ctx.LineTo(new Point(_animatedPoints.First().X, chartRect.Bottom));
            }
            
            context.DrawGeometry(FillBrush, null, pathGeometry);
            
            // 绘制线条
            var linePen = new Pen(LineBrush, LineThickness);
            var linePath = new StreamGeometry();
            using (var ctx = linePath.Open())
            {
                ctx.BeginFigure(_animatedPoints[0], false);
                for (int i = 1; i < _animatedPoints.Count; i++)
                {
                    ctx.LineTo(_animatedPoints[i]);
                }
            }
            context.DrawGeometry(null, linePen, linePath);
            
            // 绘制数据点
            for (int i = 0; i < _animatedPoints.Count; i++)
            {
                var point = _animatedPoints[i];
                var size = i == _hoveredIndex ? HoverPointSize : PointSize;
                var brush = i == _hoveredIndex ? Brushes.White : LineBrush;
                
                context.DrawEllipse(brush, null, point, size, size);
            }
        }
        
        private void DrawTooltip(DrawingContext context, Rect chartRect)
        {
            if (_hoveredIndex < 0 || _hoveredIndex >= DataPoints.Count)
                return;
            
            var point = _animatedPoints[_hoveredIndex];
            var data = DataPoints[_hoveredIndex];
            
            // 使用完整的时间格式转换
            var timeString = ConvertSecondsToTimeString(data.Value);
            
            // 提示框内容
            var tooltipText = $"{data.Label}\n{timeString}";
            var fontSize = 12.0;
            var padding = 8.0;
            var cornerRadius = 4.0;
            
            // 计算文本尺寸
            var text = new FormattedText(
                tooltipText,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(FontFamily, FontStyle.Normal, FontWeight.Normal, FontStretch.Normal),
                fontSize,
                Brushes.White
            );
            
            var textWidth = text.Width;
            var textHeight = text.Height;
            
            // 计算提示框位置
            double tooltipX, tooltipY;
            bool isOnRight = point.X < Bounds.Width / 2;
            
            if (isOnRight)
            {
                tooltipX = point.X + 10;
            }
            else
            {
                tooltipX = point.X - textWidth - padding * 2 - 10;
            }
            
            tooltipY = point.Y - textHeight - padding * 2;
            
            // 确保提示框在图表区域内
            tooltipX = Math.Max(Margin, Math.Min(tooltipX, Bounds.Width - Margin - textWidth - padding * 2));
            tooltipY = Math.Max(Margin, Math.Min(tooltipY, Bounds.Height - Margin - textHeight - padding * 2));
            
            // 绘制提示框背景
            var tooltipRect = new Rect(tooltipX, tooltipY, textWidth + padding * 2, textHeight + padding * 2);
            context.DrawRectangle(
                new SolidColorBrush(Color.FromArgb(230, 31, 41, 55)),
                null,
                tooltipRect,
                cornerRadius
            );
            
            // 绘制提示框边框
            context.DrawRectangle(
                null,
                new Pen(new SolidColorBrush(Color.FromArgb(100, 129, 140, 248)), 1),
                tooltipRect,
                cornerRadius
            );
            
            // 绘制提示箭头
            var arrowSize = 6.0;
            var arrowPath = new StreamGeometry();
            using (var ctx = arrowPath.Open())
            {
                if (isOnRight)
                {
                    ctx.BeginFigure(new Point(point.X + 5, point.Y), false);
                    ctx.LineTo(new Point(tooltipX - 5, point.Y));
                    ctx.LineTo(new Point(tooltipX, point.Y - arrowSize / 2));
                    ctx.LineTo(new Point(tooltipX - 5, point.Y + arrowSize / 2));
                    ctx.LineTo(new Point(point.X + 5, point.Y));
                }
                else
                {
                    ctx.BeginFigure(new Point(point.X - 5, point.Y), false);
                    ctx.LineTo(new Point(tooltipX + tooltipRect.Width + 5, point.Y));
                    ctx.LineTo(new Point(tooltipX + tooltipRect.Width, point.Y - arrowSize / 2));
                    ctx.LineTo(new Point(tooltipX + tooltipRect.Width + 5, point.Y + arrowSize / 2));
                    ctx.LineTo(new Point(point.X - 5, point.Y));
                }
            }
            context.DrawGeometry(
                new SolidColorBrush(Color.FromArgb(230, 31, 41, 55)), 
                new Pen(new SolidColorBrush(Color.FromArgb(100, 129, 140, 248)), 1), 
                arrowPath
            );
            
            context.DrawText(text, new Point(tooltipX + padding, tooltipY + padding));
        }
        
        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);
            
            _mousePosition = e.GetPosition(this);
            _isMouseOver = true;
            
            if (DataPoints == null || DataPoints.Count == 0 || _chartRect.Width <= 0)
                return;
            
            // 计算最近的数据点
            double minDistance = double.MaxValue;
            int closestIndex = -1;
            
            for (int i = 0; i < _animatedPoints.Count; i++)
            {
                var point = _animatedPoints[i];
                var distance = Math.Sqrt(
                    Math.Pow(point.X - _mousePosition.X, 2) + 
                    Math.Pow(point.Y - _mousePosition.Y, 2)
                );
                
                if (distance < minDistance && distance < 50) // 50像素的容差
                {
                    minDistance = distance;
                    closestIndex = i;
                }
            }
            
            if (closestIndex != _hoveredIndex)
            {
                _hoveredIndex = closestIndex;
                InvalidateVisual();
            }
        }
        
        protected override void OnPointerExited(PointerEventArgs e)
        {
            base.OnPointerExited(e);
            _isMouseOver = false;
            _hoveredIndex = -1;
            InvalidateVisual();
        }
        
        private void AnimationTimer_Tick(object sender, EventArgs e)
        {
            if (_animationProgress < 1.0)
            {
                _animationProgress = Math.Min(1.0, _animationProgress + 0.1);
                InvalidateVisual();
                
                if (_animationProgress >= 1.0)
                {
                    _animationTimer.Stop();
                }
            }
        }
        
        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);
            _animationTimer.Stop();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == DataPointsProperty)
            {
                _needsAnimationReset = true;
                InvalidateVisual();
            }
        }
    }
}
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Taix.Client.Servicers;
using Taix.Client.Shared.Helpers;
using Taix.Client.Shared.Servicers.Interfaces;

namespace Taix.Client.Controls.Timeline;

public class Timeline : Control
{
    private const int DaySeconds = 86400;
    private const int HourSeconds = 3600;
    private const double DefaultPixelsPerHour = 180.0;

    private int MaxDaySeconds
    {
        get
        {
            if (Date.Date != DateTime.Today) return DaySeconds;
            var now = DateTime.Now;
            var seconds = now.Hour * HourSeconds + now.Minute * 60 + now.Second;
            var hours = Math.Max(1, (seconds + HourSeconds - 1) / HourSeconds);
            return hours * HourSeconds;
        }
    }

    #region Direct Properties

    private IEnumerable<TimelineUsageItem> _usageItems = Array.Empty<TimelineUsageItem>();
    private TimelineViewMode _viewMode = TimelineViewMode.App;
    private double _zoom = 1.0;
    private int _majorTickInterval = 1;
    private DateTime _date = DateTime.Now;
    private double _visibleStartHour = 0.0;
    private double _visibleEndHour = 24.0;

    public static readonly DirectProperty<Timeline, IEnumerable<TimelineUsageItem>> UsageItemsProperty =
        AvaloniaProperty.RegisterDirect<Timeline, IEnumerable<TimelineUsageItem>>(
            nameof(UsageItems), o => o._usageItems, (o, v) => o._usageItems = v);

    public static readonly DirectProperty<Timeline, TimelineViewMode> ViewModeProperty =
        AvaloniaProperty.RegisterDirect<Timeline, TimelineViewMode>(
            nameof(ViewMode), o => o._viewMode, (o, v) => o._viewMode = v, TimelineViewMode.App);

    public static readonly DirectProperty<Timeline, double> ZoomProperty =
        AvaloniaProperty.RegisterDirect<Timeline, double>(
            nameof(Zoom), o => o._zoom, (o, v) => o._zoom = v, 1.0);

    public static readonly DirectProperty<Timeline, int> MajorTickIntervalProperty =
        AvaloniaProperty.RegisterDirect<Timeline, int>(
            nameof(MajorTickInterval), o => o._majorTickInterval, (o, v) => o._majorTickInterval = v, 1);

    public static readonly DirectProperty<Timeline, DateTime> DateProperty =
        AvaloniaProperty.RegisterDirect<Timeline, DateTime>(
            nameof(Date), o => o._date, (o, v) => o._date = v);

    public static readonly DirectProperty<Timeline, double> VisibleStartHourProperty =
        AvaloniaProperty.RegisterDirect<Timeline, double>(
            nameof(VisibleStartHour), o => o._visibleStartHour, (o, v) => o._visibleStartHour = v);

    public static readonly DirectProperty<Timeline, double> VisibleEndHourProperty =
        AvaloniaProperty.RegisterDirect<Timeline, double>(
            nameof(VisibleEndHour), o => o._visibleEndHour, (o, v) => o._visibleEndHour = v, 24.0);

    public IEnumerable<TimelineUsageItem> UsageItems
    {
        get => _usageItems;
        set => SetAndRaise(UsageItemsProperty, ref _usageItems, value);
    }

    public TimelineViewMode ViewMode
    {
        get => _viewMode;
        set => SetAndRaise(ViewModeProperty, ref _viewMode, value);
    }

    public double Zoom
    {
        get => _zoom;
        set => SetAndRaise(ZoomProperty, ref _zoom, value);
    }

    public int MajorTickInterval
    {
        get => _majorTickInterval;
        set => SetAndRaise(MajorTickIntervalProperty, ref _majorTickInterval, value);
    }

    public DateTime Date
    {
        get => _date;
        set => SetAndRaise(DateProperty, ref _date, value);
    }

    public double VisibleStartHour
    {
        get => _visibleStartHour;
        set => SetAndRaise(VisibleStartHourProperty, ref _visibleStartHour, value);
    }

    public double VisibleEndHour
    {
        get => _visibleEndHour;
        set => SetAndRaise(VisibleEndHourProperty, ref _visibleEndHour, value);
    }

    #endregion

    #region Layout

    private const double HeaderH = 22;
    private const double UsageH = 40;
    private const double GapH = 6;

    // Y positions
    private double UsageY => HeaderH + GapH;

    public new static readonly StyledProperty<double> HeightProperty =
        AvaloniaProperty.Register<Timeline, double>(nameof(Height), 68);

    #endregion

    #region Fields

    private double _offsetX;
    private bool _isPanning;
    private bool _isSelecting;
    private double _selectStartX;
    private double _selectCurrentX;
    private Point _dragStart;
    private double _dragStartOffset;
    private Point? _mousePos;
    private TimelineUsageItem? _hoveredItem;
    private IDisposable? _themeSubscription;

    #endregion

    #region Brushes

    private IBrush _bgBrush = null!;
    private IPen _borderPen = null!;
    private IBrush _headerBg = null!;
    private IBrush _inactiveBrush = null!;
    private IPen _tickMajor = null!;
    private IPen _tickHalf = null!;
    private IPen _tickQuarter = null!;
    private IBrush _tickText = null!;
    private IBrush _timeLabelText = null!;
    private IPen _nowPen = null!;
    private IBrush _selectBg = null!;
    private IPen _selectPen = null!;
    private IBrush _tipBg = null!;
    private IPen _tipPen = null!;
    private IBrush _tipText = null!;
    private IBrush _tipSub = null!;
    private IBrush _trackBgBrush = null!;
    private IBrush _tooltipShadow = null!;
    private Color _idleColor;
    private Color _defaultColor;
    private IBrush _nowDotOuterBrush = null!;
    private IBrush _periodNightBrush = null!;
    private IBrush _periodMorningBrush = null!;
    private IBrush _periodNoonBrush = null!;
    private IBrush _periodAfternoonBrush = null!;
    private IBrush _periodEveningBrush = null!;

    // 框选新样式笔刷
    private IBrush _selectionMaskBrush = null!;
    private IPen _selectionBorderPen = null!;
    private IBrush _selectionGlowBrush = null!;
    private IBrush _selectionHandleBrush = null!;
    private IPen _selectionHandleBorderPen = null!;
    private IBrush _selectionLabelBgBrush = null!;
    private IBrush _selectionLabelTextBrush = null!;

    // 拖拽手柄状态
    private enum DragHandle { None, Left, Right }
    private DragHandle _activeHandle = DragHandle.None;
    private bool _isDraggingHandle;
    private bool _suppressFitToRange;
    private bool _wasDragging;
    private static readonly Cursor HandleCursor = new(StandardCursorType.SizeWestEast);

    #endregion

    public Timeline()
    {
        ClipToBounds = true;
        Height = 68;
        LoadThemeBrushes();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        LoadThemeBrushes();
        var appConfig = ServiceLocator.GetService<IAppConfig>();
        if (appConfig != null)
        {
            _themeSubscription = appConfig.WhenAnyThemeRelatedChanged(() =>
            {
                LoadThemeBrushes();
                InvalidateVisual();
            });
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _themeSubscription?.Dispose();
    }

    private void LoadThemeBrushes()
    {
        _bgBrush = FindBrush("TimelineBgBrush", Color.Parse("#0d1117"));
        _borderPen = new Pen(FindBrush("TimelineBorderBrush", Color.Parse("#21262d")), 1);
        _headerBg = FindBrush("TimelineHeaderBgBrush", Color.Parse("#161b22"), 0.7);
        _inactiveBrush = FindBrush("TimelineInactiveBrush", Color.Parse("#30363d"));
        _tickMajor = new Pen(FindBrush("TimelineTickMajorBrush", Color.Parse("#30363d")), 1);
        _tickHalf = new Pen(FindBrush("TimelineTickHalfBrush", Color.Parse("#21262d")), 0.5);
        _tickQuarter = new Pen(FindBrush("TimelineTickQuarterBrush", Color.Parse("#1a1f27")), 0.5);
        _tickText = FindBrush("TimelineTickTextBrush", Color.Parse("#8b949e"));
        _timeLabelText = FindBrush("TimelineTimeLabelBrush", Color.Parse("#8b949e"), 0.85);
        _nowPen = new Pen(FindBrush("TimelineNowBrush", Color.Parse("#58a6ff")), 1.5);
        _selectBg = FindBrush("TimelineSelectBgBrush", Color.Parse("#58a6ff"), 0.12);
        _selectPen = new Pen(FindBrush("TimelineSelectBorderBrush", Color.Parse("#58a6ff")), 1.5);
        _tipBg = FindBrush("TimelineTipBgBrush", Color.Parse("#161b22"));
        _tipPen = new Pen(FindBrush("TimelineTipBorderBrush", Color.Parse("#30363d")), 1);
        _tipText = FindBrush("TimelineTipTextBrush", Colors.White);
        _tipSub = FindBrush("TimelineTipSubBrush", Color.Parse("#8b949e"));
        _trackBgBrush = FindBrush("TimelineTrackBgBrush", Color.Parse("#252525"));
        _tooltipShadow = FindBrush("TimelineTooltipShadowBrush", Color.Parse("#000000"), 0.5);
        _idleColor = FindColor("TimelineIdleColor", Color.Parse("#484f58"));
        _defaultColor = FindColor("TimelineDefaultColor", Color.Parse("#888888"));
        _nowDotOuterBrush = FindBrush("TimelineNowDotOuterBrush", Colors.White);
        _periodNightBrush = FindBrush("TimelinePeriodNightBgBrush", Color.Parse("#14182e"));
        _periodMorningBrush = FindBrush("TimelinePeriodMorningBgBrush", Color.Parse("#142014"));
        _periodNoonBrush = FindBrush("TimelinePeriodNoonBgBrush", Color.Parse("#1c1c24"));
        _periodAfternoonBrush = FindBrush("TimelinePeriodAfternoonBgBrush", Color.Parse("#241c14"));
        _periodEveningBrush = FindBrush("TimelinePeriodEveningBgBrush", Color.Parse("#1c1424"));
        _selectionMaskBrush = FindBrush("TimelineSelectionMaskBrush", Color.Parse("#000000"), 0.5);
        _selectionBorderPen = new Pen(FindBrush("TimelineSelectionBorderBrush", Colors.White), 1.5);
        _selectionGlowBrush = FindBrush("TimelineSelectionGlowBrush", Colors.White, 0.12);
        _selectionHandleBrush = FindBrush("TimelineSelectionHandleBrush", Colors.White);
        _selectionHandleBorderPen = new Pen(FindBrush("TimelineSelectionHandleBorderBrush", Color.Parse("#58a6ff")), 2);
        _selectionLabelBgBrush = FindBrush("TimelineSelectionLabelBgBrush", Color.Parse("#161b22"));
        _selectionLabelTextBrush = FindBrush("TimelineSelectionLabelTextBrush", Colors.White);
    }

    private static IBrush FindBrush(string key, Color fallback, double opacity = 1.0)
    {
        var brush = Application.Current?.FindResource(key) as IBrush;
        if (brush != null) return brush;
        return new SolidColorBrush(fallback, opacity);
    }

    private static Color FindColor(string key, Color fallback)
    {
        if (Application.Current?.FindResource(key) is Color c) return c;
        if (Application.Current?.FindResource(key) is SolidColorBrush b) return b.Color;
        return fallback;
    }

    private double Pps => (DefaultPixelsPerHour / HourSeconds) * Zoom;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == UsageItemsProperty
            || change.Property == ViewModeProperty
            || change.Property == ZoomProperty
            || change.Property == DateProperty
            || change.Property == MajorTickIntervalProperty
            || change.Property == VisibleStartHourProperty
            || change.Property == VisibleEndHourProperty)
        {
            if (change.Property == UsageItemsProperty) FitToData();
            if ((change.Property == VisibleStartHourProperty
                || change.Property == VisibleEndHourProperty)
                && !_suppressFitToRange)
                FitToVisibleRange();
            InvalidateVisual();
        }
    }

    private void FitToData()
    {
        var items = UsageItems?.Where(i => !IsIdle(i)).ToList();
        if (items == null || items.Count == 0) { _offsetX = 0; return; }

        var firstSeconds = items.Min(i => i.Start.Hour * 3600 + i.Start.Minute * 60);
        var lastSeconds = items.Max(i => i.End.Hour * 3600 + i.End.Minute * 60);
        firstSeconds = Math.Max(0, firstSeconds - HourSeconds);
        lastSeconds = Math.Min(MaxDaySeconds, lastSeconds + HourSeconds);

        var pps = Pps;
        if (Bounds.Width > 0)
        {
            var target = -(firstSeconds * pps) + Bounds.Width * 0.05;
            _offsetX = Math.Clamp(target, Math.Min(0, Bounds.Width - MaxDaySeconds * pps), 0);
        }
    }

    #region Input

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            var maxHour = (double)MaxDaySeconds / HourSeconds;
            if (VisibleStartHour > 0.0 || VisibleEndHour < maxHour)
            {
                ResetSelection();
                e.Handled = true;
            }
        }
        base.OnKeyDown(e);
    }

    private void ResetSelection()
    {
        var maxHour = (double)MaxDaySeconds / HourSeconds;
        VisibleStartHour = 0.0;
        VisibleEndHour = maxHour;
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        e.Handled = true;
        var px = e.GetPosition(this).X;
        var oldZ = Zoom;
        var newZ = Math.Clamp(oldZ * (e.Delta.Y > 0 ? 1.15 : 0.85), 0.3, 50);
        Zoom = newZ;
        var tw = MaxDaySeconds * Pps;
        var minOffset = Math.Min(0, Bounds.Width - tw);
        var maxOffset = Math.Max(0, Bounds.Width - tw);
        _offsetX = Math.Clamp(px - (px - _offsetX) * (newZ / oldZ), minOffset, maxOffset);
        InvalidateVisual();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        var pos = e.GetPosition(this);
        _mousePos = pos;

        if (e.ClickCount == 2)
        {
            ResetSelection();
            e.Handled = true;
            return;
        }

        // 优先检测手柄点击
        var handle = HitTestHandle(pos);
        if (handle != DragHandle.None)
        {
            _isDraggingHandle = true;
            _activeHandle = handle;
            _isPanning = false;
            _isSelecting = false;
            e.Pointer.Capture(this);
            InvalidateVisual();
            return;
        }

        _dragStart = pos;
        _wasDragging = false;

        if (e.KeyModifiers == KeyModifiers.Shift)
        {
            _isSelecting = true;
            _isPanning = false;
            _selectStartX = pos.X;
            _selectCurrentX = pos.X;
        }
        else
        {
            _isPanning = true;
            _isSelecting = false;
            _dragStartOffset = _offsetX;
        }

        e.Pointer.Capture(this);
        InvalidateVisual();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        var pos = e.GetPosition(this);
        _mousePos = pos;

        if (_isSelecting)
        {
            _selectCurrentX = pos.X;
            InvalidateVisual();
        }
        else if (_isDraggingHandle)
        {
            var pps = Pps;
            var sec = (pos.X - _offsetX) / pps;
            sec = Math.Clamp(sec, 0, MaxDaySeconds);
            var hour = sec / HourSeconds;

            _suppressFitToRange = true;
            const double minSelectionHours = 1.0 / 60.0; // 最小允许 1 分钟
            if (_activeHandle == DragHandle.Left)
            {
                var endHour = VisibleEndHour;
                if (hour < endHour - minSelectionHours)
                    VisibleStartHour = Math.Max(0, hour);
            }
            else if (_activeHandle == DragHandle.Right)
            {
                var startHour = VisibleStartHour;
                var maxHour = (double)MaxDaySeconds / HourSeconds;
                if (hour > startHour + minSelectionHours)
                    VisibleEndHour = Math.Min(maxHour, hour);
            }
            _suppressFitToRange = false;
            // setter 触发重绘
        }
        else if (_isPanning)
        {
            if (!_wasDragging)
            {
                if (Math.Abs(pos.X - _dragStart.X) > 3 || Math.Abs(pos.Y - _dragStart.Y) > 3)
                    _wasDragging = true;
            }
            var tw = MaxDaySeconds * Pps;
            var minOffset = Math.Min(0, Bounds.Width - tw);
            var maxOffset = Math.Max(0, Bounds.Width - tw);
            _offsetX = Math.Clamp(_dragStartOffset + pos.X - _dragStart.X, minOffset, maxOffset);
            InvalidateVisual();
        }
        else
        {
            var prevHovered = _hoveredItem;
            UpdateHover(pos);
            var handle = HitTestHandle(pos);
            Cursor = handle != DragHandle.None ? HandleCursor : null;

            // hover 项变化时重绘
            if (_hoveredItem != prevHovered)
            {
                InvalidateVisual();
            }
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        var pos = e.GetPosition(this);

        if (_isDraggingHandle)
        {
            _isDraggingHandle = false;
            _activeHandle = DragHandle.None;
        }
        else if (_isSelecting)
        {
            var pps = Pps;
            var x1 = Math.Min(_selectStartX, _selectCurrentX);
            var x2 = Math.Max(_selectStartX, _selectCurrentX);

            if (x2 - x1 > 5) // 至少5像素才视为有效框选
            {
                var startSec = (x1 - _offsetX) / pps;
                var endSec = (x2 - _offsetX) / pps;

                startSec = Math.Clamp(startSec, 0, MaxDaySeconds);
                endSec = Math.Clamp(endSec, 0, MaxDaySeconds);

                // 最小选择时长：取1分钟和当前可视宽度2%的较小值，允许框选到很小范围
                var visibleDuration = Bounds.Width / pps;
                var minDuration = Math.Min(60, visibleDuration * 0.02);
                if (endSec - startSec < minDuration)
                {
                    var mid = (startSec + endSec) / 2;
                    startSec = Math.Max(0, mid - minDuration / 2);
                    endSec = startSec + minDuration;
                    if (endSec > MaxDaySeconds)
                    {
                        endSec = MaxDaySeconds;
                        startSec = Math.Max(0, endSec - minDuration);
                    }
                }

                var startHour = startSec / HourSeconds;
                var maxHour = (double)MaxDaySeconds / HourSeconds;
                var endHour = Math.Min(maxHour, endSec / HourSeconds);
                const double minSelectionHours = 1.0 / 60.0;
                if (endHour <= startHour) endHour = Math.Min(maxHour, startHour + minSelectionHours);

                VisibleStartHour = startHour;
                VisibleEndHour = endHour;
            }
        }
        else if (_isPanning && !_wasDragging)
        {
            // 单击空白处取消框选
            var pps = Pps;
            var startSec = VisibleStartHour * HourSeconds;
            var endSec = VisibleEndHour * HourSeconds;
            var selX1 = startSec * pps + _offsetX;
            var selX2 = endSec * pps + _offsetX;
            var maxHour = (double)MaxDaySeconds / HourSeconds;
            if ((VisibleStartHour > 0.0 || VisibleEndHour < maxHour)
                && (pos.X < selX1 || pos.X > selX2))
            {
                ResetSelection();
            }
        }

        _isPanning = false;
        _isSelecting = false;
        _wasDragging = false;
        e.Pointer.Capture(null);
        InvalidateVisual();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        _mousePos = null; _hoveredItem = null;
        InvalidateVisual();
    }

    private void UpdateHover(Point pos)
    {
        var sec = (pos.X - _offsetX) / Pps;
        if (sec < 0 || sec > DaySeconds) { _hoveredItem = null; return; }
        var t = Date.Date.AddSeconds(sec);
        var dayStart = Date.Date;
        var dayEnd = dayStart.AddDays(1);
        var items = UsageItems;
        if (items == null) { _hoveredItem = null; return; }

        if (ViewMode == TimelineViewMode.Category)
        {
            items = MergeCategory(items.ToList());
        }

        foreach (var r in items)
        {
            var effectiveStart = r.Start < dayStart ? dayStart : r.Start;
            var effectiveEnd = r.End > dayEnd ? dayEnd : r.End;
            if (effectiveEnd > effectiveStart && t >= effectiveStart && t < effectiveEnd)
            {
                _hoveredItem = r;
                return;
            }
        }
        _hoveredItem = null;
    }

    #endregion

    #region Render

    public override void Render(DrawingContext ctx)
    {
        var b = Bounds;
        if (b.Width <= 0 || b.Height <= 0) return;
        var pps = Pps;

        using (ctx.PushClip(b))
        {
            DrawTimePeriodBackgrounds(ctx, pps);
            DrawTicks(ctx, pps);
            DrawNow(ctx, pps);

            if (ViewMode == TimelineViewMode.App)
                DrawUsageTrack(ctx, pps);
            else
                DrawCategoryTrack(ctx, pps);

            if (!_isSelecting)
                DrawSelectedRange(ctx, pps);
            else
                DrawSelectionBox(ctx);

            if (!_isPanning && !_isSelecting && _mousePos.HasValue)
                DrawTooltip(ctx);
        }
    }

    private void DrawTimePeriodBackgrounds(DrawingContext ctx, double pps)
    {
        var maxSeconds = MaxDaySeconds;
        var periods = new (int Start, int End, IBrush Brush)[]
        {
            (0, 6, _periodNightBrush),
            (6, 12, _periodMorningBrush),
            (12, 14, _periodNoonBrush),
            (14, 18, _periodAfternoonBrush),
            (18, 24, _periodEveningBrush),
        };

        foreach (var (start, end, brush) in periods)
        {
            var startSec = start * HourSeconds;
            var endSec = Math.Min(end * HourSeconds, maxSeconds);
            if (endSec <= startSec) continue;

            var x1 = startSec * pps + _offsetX;
            var x2 = endSec * pps + _offsetX;

            if (x2 < 0 || x1 > Bounds.Width) continue;

            x1 = Math.Max(0, x1);
            x2 = Math.Min(Bounds.Width, x2);

            if (x2 > x1)
            {
                ctx.DrawRectangle(brush, null, new Rect(x1, UsageY, x2 - x1, UsageH));
            }
        }
    }

    private void DrawTicks(DrawingContext ctx, double pps)
    {
        var maxHours = MaxDaySeconds / HourSeconds;
        var interval = Math.Max(1, MajorTickInterval);
        var pixelsPerHour = HourSeconds * pps;

        // 自动调整主刻度间隔，确保标签不会太密集（至少 70 像素一个标签）
        var pixelsPerInterval = interval * HourSeconds * pps;
        while (pixelsPerInterval < 70 && interval < maxHours)
        {
            interval++;
            pixelsPerInterval = interval * HourSeconds * pps;
        }

        var trackTop = UsageY;

        // 根据缩放级别确定要显示的细粒度刻度（避免拥挤）
        int? fineTickMinutes = null;
        if (pixelsPerHour > 480) fineTickMinutes = 5;      // zoom > ~2.7
        else if (pixelsPerHour > 240) fineTickMinutes = 10; // zoom > ~1.3
        else if (pixelsPerHour > 120) fineTickMinutes = 15; // zoom > ~0.7

        for (var h = 0; h <= maxHours; h++)
        {
            var x = h * HourSeconds * pps + _offsetX;
            if (x < -2 || x > Bounds.Width + 2) continue;

            var isMajor = h % interval == 0;

            if (isMajor)
            {
                // 主刻度线（贯穿整个高度）
                ctx.DrawLine(_tickMajor, new Point(x, HeaderH - 4), new Point(x, Bounds.Height));

                var label = new FormattedText($"{h:D2}:00", CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.Medium),
                    10, _tickText);

                // 修复标签被裁剪：左侧不超出边界
                var labelX = Math.Max(2, x + 4);
                // 右侧也不超出边界
                if (labelX + label.Width > Bounds.Width - 2)
                    labelX = Math.Max(2, Bounds.Width - 2 - label.Width);

                ctx.DrawText(label, new Point(labelX, 4));
            }
            else
            {
                // 每小时辅助刻度线（仅在轨道区）
                ctx.DrawLine(_tickHalf, new Point(x, trackTop), new Point(x, Bounds.Height));
            }

            // 半小时辅助刻度（更淡）- 始终显示
            if (h < maxHours)
            {
                var hx = (h * HourSeconds + 1800) * pps + _offsetX;
                if (hx >= 0 && hx <= Bounds.Width)
                    ctx.DrawLine(_tickQuarter, new Point(hx, trackTop + 8), new Point(hx, Bounds.Height));
            }

            // 更细粒度的时间刻度（15/10/5分钟）
            if (fineTickMinutes.HasValue && h < maxHours)
            {
                var stepSeconds = fineTickMinutes.Value * 60;
                for (var s = stepSeconds; s < HourSeconds; s += stepSeconds)
                {
                    // 半小时刻度已单独绘制，跳过避免重复
                    if (s == 1800) continue;
                    var sx = (h * HourSeconds + s) * pps + _offsetX;
                    if (sx >= 0 && sx <= Bounds.Width)
                    {
                        // 更细的刻度线更短
                        var tickTop = trackTop + (fineTickMinutes.Value <= 10 ? 14 : 10);
                        ctx.DrawLine(_tickQuarter, new Point(sx, tickTop), new Point(sx, Bounds.Height));
                    }
                }
            }
        }

        // 当缩放足够大时，在分钟位置显示标签
        DrawMinuteLabels(ctx, pps, maxHours);
    }

    private void DrawMinuteLabels(DrawingContext ctx, double pps, int maxHours)
    {
        var pixelsPerMinute = 60 * pps;
        if (pixelsPerMinute < 6) return; // 每分钟至少6像素才显示分钟标签

        // 确定分钟标签间隔，确保标签间距至少约 50 像素，同时避免相互叠加
        int labelIntervalMinutes;
        if (pixelsPerMinute > 18) labelIntervalMinutes = 5;
        else if (pixelsPerMinute > 9) labelIntervalMinutes = 10;
        else labelIntervalMinutes = 15;

        var stepSeconds = labelIntervalMinutes * 60;
        var font = new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.Normal);

        for (var h = 0; h < maxHours; h++)
        {
            for (var s = stepSeconds; s < HourSeconds; s += stepSeconds)
            {
                var sx = (h * HourSeconds + s) * pps + _offsetX;
                if (sx < -20 || sx > Bounds.Width + 20) continue;

                var minutes = s / 60;
                var labelText = labelIntervalMinutes == 5 && minutes % 15 != 0
                    ? $"{minutes:D2}"  // 5分钟间隔且不是15的倍数时，只显示数字
                    : $":{minutes:D2}";

                var label = new FormattedText(labelText, CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight, font, 9, _timeLabelText);

                var labelX = sx - label.Width / 2;
                if (labelX < 2) labelX = 2;
                if (labelX + label.Width > Bounds.Width - 2) labelX = Bounds.Width - 2 - label.Width;

                ctx.DrawText(label, new Point(labelX, 14));
            }
        }
    }

    private void DrawNow(DrawingContext ctx, double pps)
    {
        var now = DateTime.Now;
        if (now.Date != Date.Date) return;
        var x = (now.Hour * 3600 + now.Minute * 60 + now.Second) * pps + _offsetX;
        if (x < 0 || x > Bounds.Width) return;

        ctx.DrawLine(_nowPen, new Point(x, HeaderH), new Point(x, Bounds.Height));

        // 圆点指示器
        ctx.DrawRectangle(_nowDotOuterBrush, null,
            new Rect(x - 3, HeaderH + 1, 6, 6), 3, 3);
        ctx.DrawRectangle(_nowPen.Brush, null,
            new Rect(x - 2.5, HeaderH + 1.5, 5, 5), 2.5, 2.5);
    }

    private void DrawUsageTrack(DrawingContext ctx, double pps)
    {
        var items = (UsageItems ?? []).ToList();
        if (items.Count == 0) return;

        var dayStart = Date.Date;
        var dayEnd = dayStart.AddDays(1);

        foreach (var item in items)
        {
            var drawStart = item.Start < dayStart ? dayStart : item.Start;
            var drawEnd = item.End > dayEnd ? dayEnd : item.End;
            if (drawEnd <= drawStart) continue;

            var x = TimeToX(drawStart, pps);
            var w = Math.Max(DurationToW(drawStart, drawEnd, pps), 1);
            if (x + w < 0 || x > Bounds.Width) continue;

            var isShort = item.IsShortSession;
            var h = isShort ? UsageH * 0.3 : UsageH;
            var y = UsageY + (UsageH - h) / 2;
            var r = new Rect(x, y, w, h);

            if (IsIdle(item))
            {
                ctx.DrawRectangle(_trackBgBrush, null, r);
            }
            else
            {
                var c = GetItemDisplayColor(item);
                var alpha = isShort ? (byte)0x80 : (byte)0xDD;
                ctx.DrawRectangle(new ImmutableSolidColorBrush(Color.FromArgb(alpha, c.R, c.G, c.B)), null, r);
            }
        }
    }

    private void DrawCategoryTrack(DrawingContext ctx, double pps)
    {
        var merged = MergeCategory((UsageItems ?? []).ToList());
        if (merged.Count == 0) return;

        var dayStart = Date.Date;
        var dayEnd = dayStart.AddDays(1);

        foreach (var item in merged)
        {
            var drawStart = item.Start < dayStart ? dayStart : item.Start;
            var drawEnd = item.End > dayEnd ? dayEnd : item.End;
            if (drawEnd <= drawStart) continue;

            var x = TimeToX(drawStart, pps);
            var w = Math.Max(DurationToW(drawStart, drawEnd, pps), 1);
            if (x + w < 0 || x > Bounds.Width) continue;
            var r = new Rect(x, UsageY, w, UsageH);
            var c = GetItemDisplayColor(item);
            ctx.DrawRectangle(new ImmutableSolidColorBrush(Color.FromArgb(0xDD, c.R, c.G, c.B)), null, r);
        }
    }

    private void DrawTooltip(DrawingContext ctx)
    {
        if (!_mousePos.HasValue || _hoveredItem == null) return;

        var tw = 180; var th = 52; var p = 8;
        var mx = _mousePos.Value.X; var my = _mousePos.Value.Y;
        var x = Math.Min(mx + 12, Bounds.Width - tw - 8);
        var y = Math.Max(my - th - 8, 4);

        ctx.DrawRectangle(_tooltipShadow, null,
            new Rect(x + 2, y + 2, tw, th), 6, 6);
        ctx.DrawRectangle(_tipBg, _tipPen, new Rect(x, y, tw, th), 6, 6);

        var c = GetItemDisplayColor(_hoveredItem);
        ctx.DrawRectangle(new ImmutableSolidColorBrush(c), null,
            new Rect(x + 2, y + 2, 3, th - 4), 2, 2);

        var title = new FormattedText(_hoveredItem.Name, CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold),
            12, _tipText);
        ctx.DrawText(title, new Point(x + p + 6, y + p));

        var dur = (int)(_hoveredItem.End - _hoveredItem.Start).TotalSeconds;
        var durStr = dur switch
        {
            >= 3600 => $"{dur / 3600}h{(dur % 3600) / 60}m",
            >= 60 => $"{dur / 60}m{dur % 60}s",
            _ => $"{dur}s"
        };
        var info = new FormattedText(
            $"{_hoveredItem.Start:HH:mm} – {_hoveredItem.End:HH:mm} · {durStr}",
            CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface(FontFamily.Default), 10, _tipSub);
        ctx.DrawText(info, new Point(x + p + 6, y + p + 22));
    }

    #endregion

    #region Helpers

    private double TimeToX(DateTime t, double pps) =>
        (t.Hour * 3600 + t.Minute * 60 + t.Second) * pps + _offsetX;

    private double DurationToW(DateTime s, DateTime e, double pps) => (e - s).TotalSeconds * pps;

    private List<TimelineUsageItem> MergeCategory(List<TimelineUsageItem> items)
    {
        var merged = new List<TimelineUsageItem>();
        foreach (var item in items.OrderBy(i => i.Start))
        {
            if (IsIdle(item)) continue;
            var last = merged.LastOrDefault();
            // 只有真正相邻或重叠的同分类段才合并，避免吞掉中间的空闲/其他分类间隙
            if (last != null && last.CategoryName == item.CategoryName && item.Start <= last.End)
                last.End = item.End;
            else
                merged.Add(new TimelineUsageItem
                {
                    Name = item.CategoryName, Color = item.CategoryColor,
                    CategoryName = item.CategoryName, CategoryColor = item.CategoryColor,
                    Start = item.Start, End = item.End, Data = item.Data
                });
        }
        return merged;
    }

    private bool IsIdle(TimelineUsageItem i) => TimelineHelpers.IsIdleItem(i);

    private Color ParseColor(string? hex)
    {
        if (string.IsNullOrEmpty(hex)) return _defaultColor;
        try { return Color.Parse(hex); } catch { return _defaultColor; }
    }

    private Color GetItemDisplayColor(TimelineUsageItem item)
    {
        if (IsIdle(item)) return _idleColor;
        return ParseColor(item.Color);
    }

    #endregion

    private void DrawSelectionBox(DrawingContext ctx)
    {
        var x1 = Math.Min(_selectStartX, _selectCurrentX);
        var x2 = Math.Max(_selectStartX, _selectCurrentX);
        if (x2 <= x1) return;

        var b = Bounds;

        // 200: 外部遮罩层（压暗非选中区）
        if (x1 > 0)
            ctx.DrawRectangle(_selectionMaskBrush, null, new Rect(0, 0, x1, b.Height));
        if (x2 < b.Width)
            ctx.DrawRectangle(_selectionMaskBrush, null, new Rect(x2, 0, b.Width - x2, b.Height));

        // 201: 选中框本体（白色边框 + 微弱内发光）
        var glowRect = new Rect(x1 - 1, UsageY - 1, x2 - x1 + 2, b.Height - UsageY + 2);
        ctx.DrawRectangle(_selectionGlowBrush, null, glowRect);

        var rect = new Rect(x1, UsageY, x2 - x1, b.Height - UsageY);
        ctx.DrawRectangle(_selectBg, _selectionBorderPen, rect);
    }

    private void DrawSelectedRange(DrawingContext ctx, double pps)
    {
        var maxHour = (double)MaxDaySeconds / HourSeconds;
        var startHour = VisibleStartHour;
        var endHour = VisibleEndHour;
        if (endHour <= startHour) return;

        var startSec = startHour * HourSeconds;
        var endSec = endHour * HourSeconds;
        var x1 = startSec * pps + _offsetX;
        var x2 = endSec * pps + _offsetX;

        // 只绘制在可视区域内的部分
        if (x2 < 0 || x1 > Bounds.Width) return;

        var drawX1 = Math.Max(0, x1);
        var drawX2 = Math.Min(Bounds.Width, x2);
        if (drawX2 <= drawX1) return;

        var b = Bounds;

        // 200: 外部遮罩层（压暗非选中区）
        if (drawX1 > 0)
            ctx.DrawRectangle(_selectionMaskBrush, null, new Rect(0, 0, drawX1, b.Height));
        if (drawX2 < b.Width)
            ctx.DrawRectangle(_selectionMaskBrush, null, new Rect(drawX2, 0, b.Width - drawX2, b.Height));

        // 201: 选中框本体（白色边框 + 微弱内发光）
        var glowRect = new Rect(drawX1 - 1, UsageY - 1, drawX2 - drawX1 + 2, b.Height - UsageY + 2);
        ctx.DrawRectangle(_selectionGlowBrush, null, glowRect);

        var rect = new Rect(drawX1, UsageY, drawX2 - drawX1, b.Height - UsageY);
        ctx.DrawRectangle(_selectBg, _selectionBorderPen, rect);

        // 202: 边界时间标签（悬浮在框上方）
        DrawSelectionTimeLabels(ctx, startSec, endSec, drawX1, drawX2);

        // 202.5: 框选范围内的小时分割线
        var splitStartHour = (int)Math.Ceiling(startSec / HourSeconds);
        var splitEndHour = (int)Math.Floor(endSec / HourSeconds);
        var hourFont = new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.Normal);
        for (var h = splitStartHour; h <= splitEndHour; h++)
        {
            var hx = h * HourSeconds * pps + _offsetX;
            if (hx < drawX1 || hx > drawX2) continue;

            // 垂直虚线
            var dashPen = new Pen(_selectionBorderPen.Brush, 0.8)
            {
                DashStyle = new DashStyle([3, 3], 0)
            };
            ctx.DrawLine(dashPen, new Point(hx, UsageY), new Point(hx, b.Height));

            // 小时标签
            var hourTime = Date.Date.AddHours(h);
            var hourLabel = new FormattedText(hourTime.ToString("HH:mm"), CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, hourFont, 9, _selectionLabelTextBrush);
            var hourLabelW = hourLabel.Width + 4;
            var hourLabelX = hx - hourLabelW / 2;
            hourLabelX = Math.Max(drawX1 + 2, Math.Min(drawX2 - hourLabelW - 2, hourLabelX));

            ctx.DrawRectangle(_selectionLabelBgBrush, null, new Rect(hourLabelX, UsageY + 2, hourLabelW, 14), 3, 3);
            ctx.DrawText(hourLabel, new Point(hourLabelX + 2, UsageY + 3));
        }

        // 203: 拖拽手柄（左右圆点，可交互）
        DrawSelectionHandles(ctx, drawX1, drawX2);
    }

    private DragHandle HitTestHandle(Point pos)
    {
        var startHour = VisibleStartHour;
        var endHour = VisibleEndHour;
        if (endHour <= startHour) return DragHandle.None;

        var pps = Pps;
        var startSec = startHour * HourSeconds;
        var endSec = endHour * HourSeconds;
        var x1 = startSec * pps + _offsetX;
        var x2 = endSec * pps + _offsetX;

        if (x2 < 0 || x1 > Bounds.Width) return DragHandle.None;

        x1 = Math.Max(0, x1);
        x2 = Math.Min(Bounds.Width, x2);

        var handleRadius = 6;
        var cy = UsageY + UsageH / 2;

        if (Math.Abs(pos.X - x1) <= handleRadius + 2 && Math.Abs(pos.Y - cy) <= handleRadius + 4)
            return DragHandle.Left;
        if (Math.Abs(pos.X - x2) <= handleRadius + 2 && Math.Abs(pos.Y - cy) <= handleRadius + 4)
            return DragHandle.Right;

        return DragHandle.None;
    }

    private void DrawSelectionTimeLabels(DrawingContext ctx, double startSec, double endSec, double x1, double x2)
    {
        var startTime = Date.Date.AddSeconds(startSec);
        var endTime = Date.Date.AddSeconds(endSec);

        var font = new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold);
        const double labelHeight = 18;
        const double padding = 6;
        var y = UsageY - labelHeight - 3;
        if (y < 2) y = 2;

        // 计算总时长标签
        var durationSec = (int)(endSec - startSec);
        var durationStr = durationSec switch
        {
            < 60 => $"{durationSec}s",
            < 3600 => $"{durationSec / 60}m",
            _ => $"{durationSec / 3600}h{durationSec % 3600 / 60}m"
        };
        var durLabel = new FormattedText(durationStr, CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, font, 10, _selectionLabelTextBrush);
        var durLabelW = durLabel.Width + padding * 2;
        var durLabelX = (x1 + x2) / 2 - durLabelW / 2;

        // 起始时间标签
        var startText = startTime.ToString("HH:mm");
        var startLabel = new FormattedText(startText, CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, font, 10, _selectionLabelTextBrush);
        var startLabelW = startLabel.Width + padding * 2;
        var startLabelX = x1;
        if (startLabelX + startLabelW > Bounds.Width) startLabelX = Bounds.Width - startLabelW;
        if (startLabelX < 0) startLabelX = 0;

        var startBg = new Rect(startLabelX, y, startLabelW, labelHeight);
        ctx.DrawRectangle(_selectionLabelBgBrush, null, startBg, 4, 4);
        ctx.DrawText(startLabel, new Point(startLabelX + padding, y + (labelHeight - startLabel.Height) / 2));

        // 结束时间标签
        var endText = endTime.ToString("HH:mm");
        var endLabel = new FormattedText(endText, CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, font, 10, _selectionLabelTextBrush);
        var endLabelW = endLabel.Width + padding * 2;
        var endLabelX = x2 - endLabelW;
        if (endLabelX < 0) endLabelX = 0;
        if (endLabelX + endLabelW > Bounds.Width) endLabelX = Bounds.Width - endLabelW;

        // 避免两个标签重叠
        if (endLabelX < startLabelX + startLabelW + 4)
        {
            endLabelX = startLabelX + startLabelW + 4;
            if (endLabelX + endLabelW > Bounds.Width)
            {
                // 空间不足时，结束标签放到起始标签下方
                endLabelX = x2 - endLabelW;
                if (endLabelX < 0) endLabelX = 0;
                var y2 = y + labelHeight + 2;
                if (y2 + labelHeight < UsageY)
                {
                    var endBg2 = new Rect(endLabelX, y2, endLabelW, labelHeight);
                    ctx.DrawRectangle(_selectionLabelBgBrush, null, endBg2, 4, 4);
                    ctx.DrawText(endLabel, new Point(endLabelX + padding, y2 + (labelHeight - endLabel.Height) / 2));
                    // 总时长标签也放到第二行
                    var durY2 = y2 + labelHeight + 2;
                    if (durY2 + labelHeight < UsageY)
                    {
                        var durBg2 = new Rect(durLabelX, durY2, durLabelW, labelHeight);
                        ctx.DrawRectangle(_selectionLabelBgBrush, null, durBg2, 4, 4);
                        ctx.DrawText(durLabel, new Point(durLabelX + padding, durY2 + (labelHeight - durLabel.Height) / 2));
                    }
                    return;
                }
            }
        }

        var endBg = new Rect(endLabelX, y, endLabelW, labelHeight);
        ctx.DrawRectangle(_selectionLabelBgBrush, null, endBg, 4, 4);
        ctx.DrawText(endLabel, new Point(endLabelX + padding, y + (labelHeight - endLabel.Height) / 2));

        // 绘制总时长标签（检查是否与左右标签重叠）
        if (durLabelX >= startLabelX + startLabelW + 4 && durLabelX + durLabelW <= endLabelX - 4)
        {
            var durBg = new Rect(durLabelX, y, durLabelW, labelHeight);
            ctx.DrawRectangle(_selectionLabelBgBrush, null, durBg, 4, 4);
            ctx.DrawText(durLabel, new Point(durLabelX + padding, y + (labelHeight - durLabel.Height) / 2));
        }
        else
        {
            // 空间不足，放到第二行
            var durY2 = y + labelHeight + 2;
            if (durY2 + labelHeight < UsageY)
            {
                durLabelX = (x1 + x2) / 2 - durLabelW / 2;
                if (durLabelX < 0) durLabelX = 0;
                if (durLabelX + durLabelW > Bounds.Width) durLabelX = Bounds.Width - durLabelW;
                var durBg2 = new Rect(durLabelX, durY2, durLabelW, labelHeight);
                ctx.DrawRectangle(_selectionLabelBgBrush, null, durBg2, 4, 4);
                ctx.DrawText(durLabel, new Point(durLabelX + padding, durY2 + (labelHeight - durLabel.Height) / 2));
            }
        }
    }

    private void DrawSelectionHandles(DrawingContext ctx, double x1, double x2)
    {
        var cy = UsageY + UsageH / 2;
        const double radius = 6;

        // 左侧手柄
        if (x1 >= -radius && x1 <= Bounds.Width + radius)
        {
            ctx.DrawRectangle(_selectionHandleBrush, _selectionHandleBorderPen,
                new Rect(x1 - radius, cy - radius, radius * 2, radius * 2), radius, radius);
        }

        // 右侧手柄
        if (x2 >= -radius && x2 <= Bounds.Width + radius)
        {
            ctx.DrawRectangle(_selectionHandleBrush, _selectionHandleBorderPen,
                new Rect(x2 - radius, cy - radius, radius * 2, radius * 2), radius, radius);
        }
    }

    private void FitToVisibleRange()
    {
        if (Bounds.Width <= 0) return;
        var startSec = Math.Max(0, VisibleStartHour * HourSeconds);
        var endSec = Math.Min(MaxDaySeconds, VisibleEndHour * HourSeconds);
        if (endSec <= startSec) { endSec = Math.Min(MaxDaySeconds, startSec + HourSeconds); }
        var targetDuration = endSec - startSec;
        if (targetDuration <= 0) return;
        var targetPps = Bounds.Width / targetDuration;
        Zoom = Math.Clamp(targetPps / (DefaultPixelsPerHour / HourSeconds), 0.3, 50);
        var pps = Pps;
        _offsetX = Math.Clamp(-(startSec * pps),
            Math.Min(0, Bounds.Width - MaxDaySeconds * pps), 0);
    }

}

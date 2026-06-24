using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace Taix.Client.Controls.Base;


public class ColorPicker : TemplatedControl
{


    public static readonly StyledProperty<Color> ColorProperty =
        AvaloniaProperty.Register<ColorPicker, Color>(nameof(Color), Colors.Red);

    public static readonly StyledProperty<byte> AlphaProperty =
        AvaloniaProperty.Register<ColorPicker, byte>(nameof(Alpha), 255);

    public static readonly StyledProperty<double> HueProperty =
        AvaloniaProperty.Register<ColorPicker, double>(nameof(Hue), 0);

    public static readonly StyledProperty<double> SaturationProperty =
        AvaloniaProperty.Register<ColorPicker, double>(nameof(Saturation), 100);

    public static readonly StyledProperty<double> LightnessProperty =
        AvaloniaProperty.Register<ColorPicker, double>(nameof(Lightness), 50);

    public static readonly StyledProperty<bool> IsAlphaVisibleProperty =
        AvaloniaProperty.Register<ColorPicker, bool>(nameof(IsAlphaVisible), true);

    public Color Color
    {
        get => GetValue(ColorProperty);
        set => SetValue(ColorProperty, value);
    }

    public byte Alpha
    {
        get => GetValue(AlphaProperty);
        set => SetValue(AlphaProperty, value);
    }

    public double Hue
    {
        get => GetValue(HueProperty);
        set => SetValue(HueProperty, value);
    }

    public double Saturation
    {
        get => GetValue(SaturationProperty);
        set => SetValue(SaturationProperty, value);
    }

    public double Lightness
    {
        get => GetValue(LightnessProperty);
        set => SetValue(LightnessProperty, value);
    }

    public bool IsAlphaVisible
    {
        get => GetValue(IsAlphaVisibleProperty);
        set => SetValue(IsAlphaVisibleProperty, value);
    }

    /// <summary>拖动开始时触发（父控件可据此阻止弹窗关闭）</summary>
    public event EventHandler? DragStarted;

    /// <summary>拖动结束时触发</summary>
    public event EventHandler? DragCompleted;

    /// <summary>颜色变更时触发（实时，包括拖动中）</summary>
    public event EventHandler? ColorChanged;

    protected override Type StyleKeyOverride => typeof(ColorPicker);

    #region 模板元素

    private Border? _spectrum;
    private Border? _spectrumThumb;
    private Border? _hueTrack;
    private Border? _hueThumb;
    private Border? _alphaTrack;
    private Border? _alphaThumb;
    private Border? _preview;
    private TextBox? _hexBox;

    #endregion

    #region 复用画刷（避免每次颜色变化时重建）

    private readonly SolidColorBrush _previewBrush = new(Colors.Red);
    private readonly LinearGradientBrush _spectrumGradient;
    private readonly LinearGradientBrush _spectrumMask;
    private readonly LinearGradientBrush _alphaGradient;

    #endregion

    #region 状态标志

    private bool _isSyncing;      // 正在 Color↔HSL 之间同步，跳过 OnPropertyChanged 的反向更新
    private DragTarget _drag = DragTarget.None;

    private enum DragTarget { None, Spectrum, Hue, Alpha }

    #endregion

    public ColorPicker()
    {
        _spectrumGradient = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
            GradientStops = [new GradientStop(Colors.White, 0), new GradientStop(Colors.Red, 1)]
        };

        _spectrumMask = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops = [new GradientStop(Colors.White, 0), new GradientStop(Colors.Transparent, 1)]
        };

        _alphaGradient = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops = [new GradientStop(Color.FromArgb(255, 0, 0, 0), 0), new GradientStop(Color.FromArgb(0, 0, 0, 0), 1)]
        };
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        _spectrum = e.NameScope.Get<Border>("SpectrumBorder");
        _spectrumThumb = e.NameScope.Get<Border>("SpectrumThumb");
        _hueTrack = e.NameScope.Get<Border>("HueSliderTrack");
        _hueThumb = e.NameScope.Get<Border>("HueSliderThumb");
        _alphaTrack = e.NameScope.Get<Border>("AlphaSliderTrack");
        _alphaThumb = e.NameScope.Get<Border>("AlphaSliderThumb");
        _preview = e.NameScope.Get<Border>("PreviewBorder");
        _hexBox = e.NameScope.Get<TextBox>("HexTextBox");

        if (_spectrum != null)
        {
            _spectrum.Background = _spectrumGradient;
            _spectrum.OpacityMask = _spectrumMask;
            _spectrum.PointerPressed += OnSpectrumPointerPressed;
            _spectrum.PointerMoved += OnPointerMoved;
            _spectrum.PointerReleased += OnPointerReleased;
        }

        if (_hueTrack != null)
        {
            _hueTrack.PointerPressed += OnHuePointerPressed;
            _hueTrack.PointerMoved += OnPointerMoved;
            _hueTrack.PointerReleased += OnPointerReleased;
        }

        if (_alphaTrack != null)
        {
            _alphaTrack.Background = _alphaGradient;
            _alphaTrack.PointerPressed += OnAlphaPointerPressed;
            _alphaTrack.PointerMoved += OnPointerMoved;
            _alphaTrack.PointerReleased += OnPointerReleased;
        }

        if (_hexBox != null)
            _hexBox.LostFocus += OnHexLostFocus;

        LayoutUpdated += OnLayoutUpdated;
        SyncAll();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (_isSyncing) return;

        if (change.Property == ColorProperty)
        {
            // 外部设置 Color → 同步 HSL + 刷新视觉
            SyncHslFromColor();
            SyncAll();
        }
        else if (change.Property == HueProperty || change.Property == SaturationProperty ||
                 change.Property == LightnessProperty || change.Property == AlphaProperty)
        {
            // HSL 滑块变化 → 同步 Color + 刷新视觉
            SyncColorFromHsl();
            SyncAll();
        }
    }

    #region 指针交互

    private void OnSpectrumPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _drag = DragTarget.Spectrum;
        DragStarted?.Invoke(this, EventArgs.Empty);
        UpdateSpectrumFromPointer(e);
        e.Pointer.Capture(_spectrum!);
        e.Handled = true;
    }

    private void OnHuePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _drag = DragTarget.Hue;
        DragStarted?.Invoke(this, EventArgs.Empty);
        UpdateHueFromPointer(e);
        e.Pointer.Capture(_hueTrack!);
        e.Handled = true;
    }

    private void OnAlphaPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _drag = DragTarget.Alpha;
        DragStarted?.Invoke(this, EventArgs.Empty);
        UpdateAlphaFromPointer(e);
        e.Pointer.Capture(_alphaTrack!);
        e.Handled = true;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        switch (_drag)
        {
            case DragTarget.Spectrum: UpdateSpectrumFromPointer(e); break;
            case DragTarget.Hue: UpdateHueFromPointer(e); break;
            case DragTarget.Alpha: UpdateAlphaFromPointer(e); break;
        }
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_drag == DragTarget.None) return;
        _drag = DragTarget.None;
        e.Pointer.Capture(null);
        DragCompleted?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateSpectrumFromPointer(PointerEventArgs e)
    {
        if (_spectrum == null || _spectrum.Bounds.Width <= 0 || _spectrum.Bounds.Height <= 0) return;
        var p = e.GetPosition(_spectrum);
        var x = Math.Clamp(p.X, 0, _spectrum.Bounds.Width);
        var y = Math.Clamp(p.Y, 0, _spectrum.Bounds.Height);

        _isSyncing = true;
        Saturation = x / _spectrum.Bounds.Width * 100;
        Lightness = 100 - y / _spectrum.Bounds.Height * 100;
        _isSyncing = false;

        SyncColorFromHsl();
        UpdateThumbPositions();
        UpdatePreviewAndHex();
    }

    private void UpdateHueFromPointer(PointerEventArgs e)
    {
        if (_hueTrack == null || _hueTrack.Bounds.Height <= 0) return;
        var y = Math.Clamp(e.GetPosition(_hueTrack).Y, 0, _hueTrack.Bounds.Height);

        _isSyncing = true;
        Hue = y / _hueTrack.Bounds.Height * 360;
        _isSyncing = false;

        SyncColorFromHsl();
        UpdateSpectrumBackground();
        UpdateThumbPositions();
        UpdatePreviewAndHex();
    }

    private void UpdateAlphaFromPointer(PointerEventArgs e)
    {
        if (_alphaTrack == null || _alphaTrack.Bounds.Height <= 0) return;
        var y = Math.Clamp(e.GetPosition(_alphaTrack).Y, 0, _alphaTrack.Bounds.Height);

        _isSyncing = true;
        Alpha = (byte)Math.Round(255 - y / _alphaTrack.Bounds.Height * 255);
        _isSyncing = false;

        UpdateAlphaThumbPosition();
        UpdatePreviewAndHex();
    }

    #endregion

    #region Hex 输入

    private void OnHexLostFocus(object? sender, RoutedEventArgs e)
    {
        if (_hexBox == null) return;
        var text = _hexBox.Text?.Trim() ?? "";
        if (text.Length == 0) { SyncHexText(); return; }
        if (!text.StartsWith('#')) text = "#" + text;

        if (TryParseHex(text, out var parsed))
        {
            _isSyncing = true;
            Color = parsed;
            Alpha = parsed.A;
            _isSyncing = false;
            SyncHslFromColor();
            SyncAll();
            ColorChanged?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            SyncHexText(); // 解析失败，恢复显示
        }
    }

    private static bool TryParseHex(string hex, out Color color)
    {
        color = Colors.Transparent;
        try
        {
            if (hex.Length == 7) // #RRGGBB
            {
                color = Color.FromRgb(
                    byte.Parse(hex.AsSpan(1, 2), NumberStyles.HexNumber),
                    byte.Parse(hex.AsSpan(3, 2), NumberStyles.HexNumber),
                    byte.Parse(hex.AsSpan(5, 2), NumberStyles.HexNumber));
                return true;
            }
            if (hex.Length == 9) // #AARRGGBB
            {
                color = Color.FromArgb(
                    byte.Parse(hex.AsSpan(1, 2), NumberStyles.HexNumber),
                    byte.Parse(hex.AsSpan(3, 2), NumberStyles.HexNumber),
                    byte.Parse(hex.AsSpan(5, 2), NumberStyles.HexNumber),
                    byte.Parse(hex.AsSpan(7, 2), NumberStyles.HexNumber));
                return true;
            }
        }
        catch { }
        return false;
    }

    #endregion

    #region Color ↔ HSL 同步

    private void SyncHslFromColor()
    {
        _isSyncing = true;
        var (h, s, l) = RgbToHsl(Color.R, Color.G, Color.B);
        Hue = h;
        Saturation = s;
        Lightness = l;
        Alpha = Color.A;
        _isSyncing = false;
    }

    private void SyncColorFromHsl()
    {
        _isSyncing = true;
        var (r, g, b) = HslToRgb(Hue, Saturation, Lightness);
        Color = Color.FromArgb(Alpha, r, g, b);
        _isSyncing = false;
        ColorChanged?.Invoke(this, EventArgs.Empty);
    }

    private static (double h, double s, double l) RgbToHsl(byte r, byte g, byte b)
    {
        var rn = r / 255.0;
        var gn = g / 255.0;
        var bn = b / 255.0;
        var max = Math.Max(rn, Math.Max(gn, bn));
        var min = Math.Min(rn, Math.Min(gn, bn));
        var delta = max - min;

        double h = 0, s = 0, l = (max + min) / 2;

        if (delta > 0)
        {
            s = l > 0.5 ? delta / (2 - max - min) : delta / (max + min);
            if (max == rn) h = 60 * (((gn - bn) / delta) % 6);
            else if (max == gn) h = 60 * (((bn - rn) / delta) + 2);
            else h = 60 * (((rn - gn) / delta) + 4);
            if (h < 0) h += 360;
        }

        return (h, s * 100, l * 100);
    }

    private static (byte r, byte g, byte b) HslToRgb(double h, double s, double l)
    {
        s /= 100;
        l /= 100;
        double r, g, b;

        if (s == 0)
        {
            r = g = b = l;
        }
        else
        {
            var c = (1 - Math.Abs(2 * l - 1)) * s;
            var x = c * (1 - Math.Abs(h / 60 % 2 - 1));
            var m = l - c / 2;

            if (h < 60) { r = c; g = x; b = 0; }
            else if (h < 120) { r = x; g = c; b = 0; }
            else if (h < 180) { r = 0; g = c; b = x; }
            else if (h < 240) { r = 0; g = x; b = c; }
            else if (h < 300) { r = x; g = 0; b = c; }
            else { r = c; g = 0; b = x; }

            r += m; g += m; b += m;
        }

        return ((byte)Math.Round(r * 255), (byte)Math.Round(g * 255), (byte)Math.Round(b * 255));
    }

    #endregion

    #region 视觉刷新

    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        // 布局完成后 Bounds 才有效，此时定位滑块指针
        UpdateThumbPositions();
    }

    /// <summary>全量刷新所有视觉元素</summary>
    private void SyncAll()
    {
        UpdateSpectrumBackground();
        UpdateAlphaGradient();
        UpdateThumbPositions();
        UpdatePreviewAndHex();
    }

    private void UpdateSpectrumBackground()
    {
        if (_spectrumGradient.GradientStops.Count >= 2)
        {
            var (r, g, b) = HslToRgb(Hue, 100, 50);
            _spectrumGradient.GradientStops[1].Color = Color.FromRgb(r, g, b);
        }
    }

    private void UpdateAlphaGradient()
    {
        if (_alphaGradient.GradientStops.Count >= 2)
        {
            var c = Color.FromArgb(255, Color.R, Color.G, Color.B);
            _alphaGradient.GradientStops[0].Color = c;
            _alphaGradient.GradientStops[1].Color = Color.FromArgb(0, c.R, c.G, c.B);
        }
    }

    private void UpdateThumbPositions()
    {
        // 色谱指针
        if (_spectrumThumb != null && _spectrum != null &&
            _spectrum.Bounds.Width > 0 && _spectrum.Bounds.Height > 0)
        {
            var x = Saturation / 100 * _spectrum.Bounds.Width;
            var y = (100 - Lightness) / 100 * _spectrum.Bounds.Height;
            Canvas.SetLeft(_spectrumThumb, x - _spectrumThumb.Bounds.Width / 2);
            Canvas.SetTop(_spectrumThumb, y - _spectrumThumb.Bounds.Height / 2);
        }

        // 色相指针
        if (_hueThumb != null && _hueTrack != null && _hueTrack.Bounds.Height > 0)
        {
            var y = Hue / 360 * _hueTrack.Bounds.Height;
            Canvas.SetTop(_hueThumb, y - _hueThumb.Bounds.Height / 2);
        }

        UpdateAlphaThumbPosition();
    }

    private void UpdateAlphaThumbPosition()
    {
        if (_alphaThumb != null && _alphaTrack != null && _alphaTrack.Bounds.Height > 0)
        {
            var y = (255 - Alpha) / 255.0 * _alphaTrack.Bounds.Height;
            Canvas.SetTop(_alphaThumb, y - _alphaThumb.Bounds.Height / 2);
        }
    }

    private void UpdatePreviewAndHex()
    {
        _previewBrush.Color = Color;
        if (_preview != null)
            _preview.Background = _previewBrush;

        SyncHexText();
    }

    private void SyncHexText()
    {
        if (_hexBox == null) return;
        _hexBox.Text = Alpha < 255
            ? $"#{Alpha:X2}{Color.R:X2}{Color.G:X2}{Color.B:X2}"
            : $"#{Color.R:X2}{Color.G:X2}{Color.B:X2}";
    }

    #endregion
}

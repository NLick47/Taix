using System;
using System.Collections.Generic;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ReactiveUI;

namespace Taix.Client.Controls.Base;

public class ColorSelect : TemplatedControl
{

    public static readonly DirectProperty<ColorSelect, List<string>> ColorsProperty =
        AvaloniaProperty.RegisterDirect<ColorSelect, List<string>>(
            nameof(Colors), o => o.Colors, (o, v) => o.Colors = v);

    public static readonly DirectProperty<ColorSelect, string> ColorProperty =
        AvaloniaProperty.RegisterDirect<ColorSelect, string>(
            nameof(Color), o => o.Color, (o, v) => o.Color = v);

    public static readonly DirectProperty<ColorSelect, bool> IsOpenProperty =
        AvaloniaProperty.RegisterDirect<ColorSelect, bool>(
            nameof(IsOpen), o => o.IsOpen, (o, v) => o.IsOpen = v);

    public static readonly DirectProperty<ColorSelect, bool> IsCustomModeProperty =
        AvaloniaProperty.RegisterDirect<ColorSelect, bool>(
            nameof(IsCustomMode), o => o.IsCustomMode, (o, v) => o.IsCustomMode = v);

    public List<string> Colors
    {
        get => _colors;
        set => SetAndRaise(ColorsProperty, ref _colors, value);
    }

    public string Color
    {
        get => _color;
        set => SetAndRaise(ColorProperty, ref _color, value);
    }

    public bool IsOpen
    {
        get => _isOpen;
        set => SetAndRaise(IsOpenProperty, ref _isOpen, value);
    }

    public bool IsCustomMode
    {
        get => _isCustomMode;
        set => SetAndRaise(IsCustomModeProperty, ref _isCustomMode, value);
    }

    public ICommand ShowSelectCommand { get; private set; }
    public ICommand ToggleCustomModeCommand { get; private set; }
    public ICommand SelectionChangedCommand { get; private set; }


    /// <summary>
    /// 颜色被确认应用时触发（点击预设颜色、或弹窗关闭时有自定义颜色变更）。
    /// 拖动调色板期间不会触发。
    /// </summary>
    public event EventHandler? OnSelected;

    protected override Type StyleKeyOverride => typeof(ColorSelect);


    private string _color = "#00FFAB";
    private List<string> _colors = new();
    private bool _isOpen;
    private bool _isCustomMode;

    private Popup? _popup;
    private ColorPicker? _colorPicker;
    private Avalonia.Controls.Window? _attachedWindow;

    /// <summary>颜色变更来自 ColorPicker（而非预设色板/外部绑定）</summary>
    private bool _isFromColorPicker;

    /// <summary>有来自 ColorPicker 的待应用颜色（弹窗关闭时触发 OnSelected）</summary>
    private bool _hasPendingColor;

    /// <summary>正在拖动 ColorPicker（阻止弹窗关闭）</summary>
    private bool _isDragging;


    public ColorSelect()
    {
        this.GetObservable(IsOpenProperty).Subscribe(OnIsOpenChanged);
        ShowSelectCommand = ReactiveCommand.Create<object>(OnShowSelect);
        ToggleCustomModeCommand = ReactiveCommand.Create<object>(OnToggleCustomMode);
        SelectionChangedCommand = ReactiveCommand.Create<string>(OnSelectionChanged);
        LoadColors();
    }


    private void OnShowSelect(object obj)
    {
        IsOpen = !IsOpen;
        IsCustomMode = false;
    }

    private void OnToggleCustomMode(object obj)
    {
        IsCustomMode = !IsCustomMode;
        // 打开自定义模式时同步 ColorPicker 为当前颜色
        if (IsCustomMode && _colorPicker != null)
            SyncColorPickerFromColor();
    }

    private void OnSelectionChanged(string color)
    {
        IsOpen = false;
        Color = color;
    }



    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _popup = e.NameScope.Get<Popup>("Popup");

        var listBox = e.NameScope.Get<ListBox>("listBox");
        listBox.SelectionChanged += OnListBoxSelectionChanged;

        _colorPicker = e.NameScope.Get<ColorPicker>("ColorPicker");
        if (_colorPicker != null)
        {
            _colorPicker.DragStarted += (_, _) => _isDragging = true;
            _colorPicker.DragCompleted += (_, _) => _isDragging = false;
            _colorPicker.ColorChanged += OnColorPickerColorChanged;
        }
    }



    private void OnColorPickerColorChanged(object? sender, EventArgs e)
    {
        if (_colorPicker == null) return;

        _isFromColorPicker = true;
        var newColor = $"#{_colorPicker.Color.R:X2}{_colorPicker.Color.G:X2}{_colorPicker.Color.B:X2}";
        Color = newColor;
        _hasPendingColor = true;

        Dispatcher.UIThread.Post(() => _isFromColorPicker = false, DispatcherPriority.Background);
    }

    private void SyncColorPickerFromColor()
    {
        if (_colorPicker == null) return;
        try
        {
            var c = Avalonia.Media.Color.Parse(Color);
            _colorPicker.Color = c;
            _colorPicker.Alpha = c.A;
        }
        catch { }
    }



    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ColorProperty)
        {
            var control = (ColorSelect)change.Sender!;
            if (string.IsNullOrEmpty(control.Color))
                control.Color = control.Colors[0];

            // 来自 ColorPicker 的变更只更新预览，不触发 OnSelected
            // 来自预设色板/外部绑定的变更立即应用
            if (_isFromColorPicker)
            {
                _hasPendingColor = true;
            }
            else
            {
                _hasPendingColor = false;
                OnSelected?.Invoke(this, EventArgs.Empty);
            }
        }
        else if (change.Property == IsOpenProperty && !_isOpen)
        {
            // 弹窗关闭：应用待保存的自定义颜色
            if (_hasPendingColor)
            {
                _hasPendingColor = false;
                OnSelected?.Invoke(this, EventArgs.Empty);
            }
            // 重置交互状态
            _isDragging = false;
            _isFromColorPicker = false;
        }
    }



    private void OnIsOpenChanged(bool isOpen)
    {
        if (isOpen)
            AttachWindowEvents();
        else
            DetachWindowEvents();
    }

    private void AttachWindowEvents()
    {
        DetachWindowEvents();

        _attachedWindow = VisualRoot as Avalonia.Controls.Window;
        if (_attachedWindow == null) return;

        _attachedWindow.AddHandler(InputElement.PointerPressedEvent, OnTopLevelPointerPressed, RoutingStrategies.Tunnel);
        _attachedWindow.PointerWheelChanged += OnTopLevelPointerWheelChanged;
    }

    private void DetachWindowEvents()
    {
        if (_attachedWindow == null) return;

        _attachedWindow.RemoveHandler(InputElement.PointerPressedEvent, OnTopLevelPointerPressed);
        _attachedWindow.PointerWheelChanged -= OnTopLevelPointerWheelChanged;
        _attachedWindow = null;
    }

    private void OnTopLevelPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_isDragging) return; // 拖动中不关闭

        var source = e.Source as Visual;
        if (source == null) return;

        // 点击在弹窗内容内不关闭
        if (IsDescendantOf(source, _popup?.Child)) return;
        // 点击在 ColorSelect 自身（色块触发器）内不关闭
        if (IsDescendantOf(source, this)) return;

        IsOpen = false;
    }

    private void OnTopLevelPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (_isDragging) return;
        IsOpen = false;
    }

    private static bool IsDescendantOf(Visual? node, Visual? ancestor)
    {
        if (node == null || ancestor == null) return false;
        if (ReferenceEquals(node, ancestor)) return true;
        var current = node.GetVisualParent();
        while (current != null)
        {
            if (ReferenceEquals(current, ancestor)) return true;
            current = current.GetVisualParent();
        }
        return false;
    }



    private void OnListBoxSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        // ColorPicker 同步颜色时绑定回写 SelectedItem 会触发此事件，忽略
        if (_isFromColorPicker || _isDragging) return;

        if (e.AddedItems.Count > 0 && e.AddedItems[0] is string selectedColor)
        {
            _hasPendingColor = false;
            Color = selectedColor;
            IsOpen = false;
        }
    }



    private void LoadColors()
    {
        Colors =
        [
            "#00FFAB", "#017aff", "#6c47ff", "#39c0c8",
            "#f96300", "#f34971", "#ff9382", "#f5c900",
            "#cdad7a", "#aabb5d", "#000000", "#2B20D9"
        ];
        Color = Colors[0];
    }

}

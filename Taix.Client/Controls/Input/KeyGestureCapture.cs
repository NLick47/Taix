using System;
using System.Collections.Generic;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using ReactiveUI;

namespace Taix.Client.Controls.Input;

public class KeyGestureCapture : TemplatedControl
{
    public static readonly StyledProperty<string?> ValueProperty =
        AvaloniaProperty.Register<KeyGestureCapture, string?>(
            nameof(Value),
            defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<string?> DefaultValueProperty =
        AvaloniaProperty.Register<KeyGestureCapture, string?>(nameof(DefaultValue));

    public static readonly StyledProperty<string?> PlaceholderProperty =
        AvaloniaProperty.Register<KeyGestureCapture, string?>(nameof(Placeholder));

    public static readonly DirectProperty<KeyGestureCapture, bool> IsCapturingProperty =
        AvaloniaProperty.RegisterDirect<KeyGestureCapture, bool>(
            nameof(IsCapturing),
            o => o.IsCapturing);

    public static readonly DirectProperty<KeyGestureCapture, ICommand> ResetCommandProperty =
        AvaloniaProperty.RegisterDirect<KeyGestureCapture, ICommand>(
            nameof(ResetCommand),
            o => o.ResetCommand);

    public static readonly DirectProperty<KeyGestureCapture, ICommand> ClearCommandProperty =
        AvaloniaProperty.RegisterDirect<KeyGestureCapture, ICommand>(
            nameof(ClearCommand),
            o => o.ClearCommand);

    public static readonly DirectProperty<KeyGestureCapture, IReadOnlyList<string>> KeyPartsProperty =
        AvaloniaProperty.RegisterDirect<KeyGestureCapture, IReadOnlyList<string>>(
            nameof(KeyParts),
            o => o.KeyParts);

    public static readonly DirectProperty<KeyGestureCapture, bool> HasValueProperty =
        AvaloniaProperty.RegisterDirect<KeyGestureCapture, bool>(
            nameof(HasValue),
            o => o.HasValue);

    private bool _isCapturing;
    private readonly ICommand _resetCommand;
    private readonly ICommand _clearCommand;
    private IReadOnlyList<string> _keyParts = Array.Empty<string>();
    private bool _hasValue;

    public KeyGestureCapture()
    {
        _resetCommand = ReactiveCommand.Create(() => { Value = DefaultValue; });
        _clearCommand = ReactiveCommand.Create(() => { Value = string.Empty; });
        UpdateKeyParts();
    }

    static KeyGestureCapture()
    {
        FocusableProperty.OverrideDefaultValue<KeyGestureCapture>(true);
        GotFocusEvent.AddClassHandler<KeyGestureCapture>((c, e) => c.OnFocusGained(e));
        LostFocusEvent.AddClassHandler<KeyGestureCapture>((c, e) => c.OnFocusLost(e));
    }

    public string? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public string? DefaultValue
    {
        get => GetValue(DefaultValueProperty);
        set => SetValue(DefaultValueProperty, value);
    }

    public string? Placeholder
    {
        get => GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    public bool IsCapturing
    {
        get => _isCapturing;
        private set => SetAndRaise(IsCapturingProperty, ref _isCapturing, value);
    }

    public ICommand ResetCommand => _resetCommand;
    public ICommand ClearCommand => _clearCommand;

    public IReadOnlyList<string> KeyParts
    {
        get => _keyParts;
        private set => SetAndRaise(KeyPartsProperty, ref _keyParts, value);
    }

    public bool HasValue
    {
        get => _hasValue;
        private set => SetAndRaise(HasValueProperty, ref _hasValue, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty)
        {
            UpdateKeyParts();
        }
    }

    private void UpdateKeyParts()
    {
        var raw = Value?.Trim();
        if (string.IsNullOrEmpty(raw))
        {
            KeyParts = Array.Empty<string>();
            HasValue = false;
            return;
        }
        var parts = raw.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        KeyParts = parts;
        HasValue = parts.Length > 0;
    }

    protected override Type StyleKeyOverride => typeof(KeyGestureCapture);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
    }

    private void OnFocusGained(RoutedEventArgs e)
    {
        IsCapturing = true;
    }

    private void OnFocusLost(RoutedEventArgs e)
    {
        IsCapturing = false;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (!IsCapturing)
        {
            base.OnKeyDown(e);
            return;
        }

        // 修饰键单独不构成快捷键
        if (IsModifierKey(e.Key))
        {
            e.Handled = true;
            return;
        }

        // Esc 取消捕获；不写入
        if (e.Key == Key.Escape && e.KeyModifiers == KeyModifiers.None)
        {
            IsCapturing = false;
            Focus();  // 维持焦点视觉
            e.Handled = true;
            return;
        }

        try
        {
            var gesture = new KeyGesture(e.Key, e.KeyModifiers);
            Value = gesture.ToString();
        }
        catch
        {
            // 某些组合 Avalonia 不允许构造（如孤立的修饰键），忽略
        }

        e.Handled = true;
    }

    private static bool IsModifierKey(Key key) => key
        is Key.LeftCtrl or Key.RightCtrl
        or Key.LeftShift or Key.RightShift
        or Key.LeftAlt or Key.RightAlt
        or Key.LWin or Key.RWin;
}

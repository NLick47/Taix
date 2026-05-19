using System;
using System.Linq;
using System.Reactive;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ReactiveUI;
using Taix.Client.Controls.Base;
using Taix.Client.Controls.Input;
using Taix.Client.Models;

namespace Taix.Client.Controls.Window;

public class DefaultWindow : Avalonia.Controls.Window
{
    private const double CustomWindowChromeTitleBarHeight = 40;

    public static readonly StyledProperty<IImage?> IconSourceProperty =
        AvaloniaProperty.Register<DefaultWindow, IImage?>(nameof(IconSource));

    public static readonly StyledProperty<bool> MaximizeVisibleProperty =
        AvaloniaProperty.Register<DefaultWindow, bool>(nameof(MaximizeVisible));

    public static readonly StyledProperty<bool> RestoreVisibleProperty =
        AvaloniaProperty.Register<DefaultWindow, bool>(nameof(RestoreVisible));

    public static readonly StyledProperty<PageContainer> PageContainerProperty =
        AvaloniaProperty.Register<DefaultWindow, PageContainer>(nameof(PageContainer));

    public static readonly StyledProperty<ToastType> ToastTypeProperty =
        AvaloniaProperty.Register<DefaultWindow, ToastType>(nameof(ToastType));


    public static readonly StyledProperty<string> DialogTitleProperty =
        AvaloniaProperty.Register<DefaultWindow, string>(nameof(DialogTitle));

    public static readonly StyledProperty<IconTypes> ToastIconProperty =
        AvaloniaProperty.Register<DefaultWindow, IconTypes>(nameof(ToastIcon));

    public static readonly StyledProperty<string> ToastContentProperty =
        AvaloniaProperty.Register<DefaultWindow, string>(nameof(ToastContent));


    public static readonly StyledProperty<string> DialogMessageProperty =
        AvaloniaProperty.Register<DefaultWindow, string>(nameof(DialogMessage));


    public static readonly StyledProperty<string> InputModalValueProperty =
        AvaloniaProperty.Register<DefaultWindow, string>(nameof(InputModalValue));

    public static readonly StyledProperty<bool> IsShowToastProperty =
        AvaloniaProperty.Register<DefaultWindow, bool>(nameof(IsShowToast));

    public static readonly StyledProperty<bool> IsCanBackProperty =
        AvaloniaProperty.Register<DefaultWindow, bool>(nameof(IsCanBack));

    public static readonly StyledProperty<bool> UseCustomWindowChromeProperty =
        AvaloniaProperty.Register<DefaultWindow, bool>(
            nameof(UseCustomWindowChrome),
            !OperatingSystem.IsMacOS());

    public static readonly StyledProperty<ConnectionStatus> ConnectionStatusProperty =
        AvaloniaProperty.Register<DefaultWindow, ConnectionStatus>(nameof(ConnectionStatus), ConnectionStatus.Checking);

    private CancellationTokenSource _toastCancellationTokenSource;
    private Button.Button CancelBtn, ConfirmBtn, InputModalCancelBtn, InputModalConfirmBtn;
    private InputBox InputModalInputBox;
    private Func<string, bool> InputModalValidFnc;
    private string? InputValue;
    private bool IsDialogConfirm;
    private bool IsShowConfirmDialog, IsShowInputModal, IsShowActionDialog;
    private int ActionDialogResult = -1;
    private Border? _statusDot;
    private EventHandler? _onLoadPagedHandler;


    private Grid titleBar;

    private Border ToastBorder, Masklayer, DialogBorder, InputModalBorder, ActionDialogBorder;
    private StackPanel ActionDialogButtonsPanel;
    private Grid ToastGrid;
    public DefaultWindow()
    {
        ApplyWindowChromeMode();

        this.WhenAnyValue(x => x.MaximizeVisible, x => x.WindowState)
            .Subscribe(values =>
            {
                var (maximizeVisible, windowState) = values;
                switch (windowState)
                {
                    case WindowState.Normal:
                        MaximizeVisible = true;
                        RestoreVisible = false;
                        break;
                    case WindowState.Maximized:
                        RestoreVisible = true;
                        MaximizeVisible = false;
                        break;
                }
            });

        MinimizeWindowCommand = ReactiveCommand.Create(() => { WindowState = WindowState.Minimized; });

        RestoreWindowCommand = ReactiveCommand.Create(() => { WindowState = WindowState.Normal; });

        MaximizeWindowCommand = ReactiveCommand.Create(() => { WindowState = WindowState.Maximized; });

        CloseWindowCommand = ReactiveCommand.Create(() => { RequestClose?.Invoke(this, EventArgs.Empty); });

        BackCommand = ReactiveCommand.Create(() =>
        {
            if (PageContainer != null)
            {
                PageContainer.Back();
                if (PageContainer.Index == 0) IsCanBack = false;
            }
        });
    }

    public bool MaximizeVisible
    {
        get => GetValue(MaximizeVisibleProperty);
        set => SetValue(MaximizeVisibleProperty, value);
    }


    public bool RestoreVisible
    {
        get => GetValue(RestoreVisibleProperty);
        set => SetValue(RestoreVisibleProperty, value);
    }

    public PageContainer PageContainer
    {
        get => GetValue(PageContainerProperty);
        set => SetValue(PageContainerProperty, value);
    }

    /// <summary>
    /// toast type
    /// </summary>
    public ToastType ToastType
    {
        get => GetValue(ToastTypeProperty);
        set => SetValue(ToastTypeProperty, value);
    }

    /// <summary>
    /// toast content
    /// </summary>
    public string ToastContent
    {
        get => (string)GetValue(ToastContentProperty);
        set => SetValue(ToastContentProperty, value);
    }

    public IconTypes ToastIcon
    {
        get => GetValue(ToastIconProperty);
        set => SetValue(ToastIconProperty, value);
    }

    /// <summary>
    /// Dialog title
    /// </summary>
    public string DialogTitle
    {
        get => (string)GetValue(DialogTitleProperty);
        set => SetValue(DialogTitleProperty, value);
    }

    /// <summary>
    /// Dialog title
    /// </summary>
    public string InputModalValue
    {
        get => (string)GetValue(InputModalValueProperty);
        set => SetValue(InputModalValueProperty, value);
    }

    /// <summary>
    /// Dialog message
    /// </summary>
    public string DialogMessage
    {
        get => (string)GetValue(DialogMessageProperty);
        set => SetValue(DialogMessageProperty, value);
    }

    public bool IsShowToast
    {
        get => GetValue(IsShowToastProperty);
        set => SetValue(IsShowToastProperty, value);
    }

    /// <summary>
    /// 是否可以返回
    /// </summary>
    public bool IsCanBack
    {
        get => GetValue(IsCanBackProperty);
        set => SetValue(IsCanBackProperty, value);
    }

    public bool UseCustomWindowChrome
    {
        get => GetValue(UseCustomWindowChromeProperty);
        set => SetValue(UseCustomWindowChromeProperty, value);
    }

    public ConnectionStatus ConnectionStatus
    {
        get => GetValue(ConnectionStatusProperty);
        set => SetValue(ConnectionStatusProperty, value);
    }

    public event EventHandler? RequestClose;

    public bool IsWindowClosed { get; private set; }

    public IImage? IconSource
    {
        get => GetValue(IconSourceProperty);
        set => SetValue(IconSourceProperty, value);
    }


    protected override Type StyleKeyOverride => typeof(DefaultWindow);


    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PageContainerProperty) OnPageContainerChanged(change);
        if (change.Property == IsCanBackProperty) OnIsCanBackChanged(change);
        if (change.Property == IsShowToastProperty) OnIsShowToastChanged(change);
        if (change.Property == ConnectionStatusProperty) UpdateConnectionStatus();
        if (change.Property == UseCustomWindowChromeProperty) ApplyWindowChromeMode();
    }

    private void ApplyWindowChromeMode()
    {
        if (UseCustomWindowChrome)
        {
            ExtendClientAreaToDecorationsHint = true;
            ExtendClientAreaTitleBarHeightHint = CustomWindowChromeTitleBarHeight;
            WindowDecorations = Avalonia.Controls.WindowDecorations.None;
            return;
        }

        ExtendClientAreaToDecorationsHint = false;
        ExtendClientAreaTitleBarHeightHint = 0;
        WindowDecorations = Avalonia.Controls.WindowDecorations.Full;
    }

    private static void OnIsShowToastChanged(AvaloniaPropertyChangedEventArgs e)
    {
        var that = e.Sender as DefaultWindow;
        if (that != null)
        {
            if (that.IsShowToast)
                _ = that.ShowToastAsync();
            else
                that.HideToast();
        }
    }

    private async Task ShowToastAsync()
    {
        if (ToastGrid == null)
            return;

        if (_toastCancellationTokenSource != null)
        {
            if (!_toastCancellationTokenSource.TryReset())
            {
                _toastCancellationTokenSource.Dispose();
                _toastCancellationTokenSource = new CancellationTokenSource();
            }
        }
        else
        {
            _toastCancellationTokenSource = new CancellationTokenSource();
        }

        ToastGrid.IsVisible = true;
        DialogBorder.IsVisible = false;
        if (!IsShowInputModal) InputModalBorder.IsVisible = false;
        ToastBorder.IsVisible = true;
        ToastBorder.RenderTransform = TransformOperations.Parse("translateY(0px)");
        Masklayer.Opacity = 0.6;
        ToastGrid.PointerPressed += ToastGrid_MouseLeftButtonDown;

        try
        {
            await Task.Delay(3000, _toastCancellationTokenSource.Token);
        }
        catch (TaskCanceledException)
        {
        }

        IsShowToast = false;
    }

    public Task<bool> ShowConfirmDialogAsync(string title, string message)
    {
        IsShowConfirmDialog = true;
        ToastGrid.IsVisible = true;
        ToastBorder.IsVisible = false;
        DialogBorder.IsVisible = true;
        InputModalBorder.IsVisible = false;

        DialogMessage = message;
        DialogTitle = title;
        DialogBorder.RenderTransform = TransformOperations.Parse("translateY(0px)");
        Masklayer.Opacity = 0;

        return Task.Run(async () =>
        {
            while (IsShowConfirmDialog) await Task.Delay(10);

            return IsDialogConfirm;
        });
    }

    public Task<int> ShowActionDialogAsync(string title, string message, string[] buttons)
    {
        IsShowActionDialog = true;
        ActionDialogResult = -1;
        ToastGrid.IsVisible = true;
        ToastBorder.IsVisible = false;
        DialogBorder.IsVisible = false;
        InputModalBorder.IsVisible = false;
        ActionDialogBorder.IsVisible = true;

        DialogMessage = message;
        DialogTitle = title;
        ActionDialogBorder.RenderTransform = TransformOperations.Parse("translateY(0px)");
        Masklayer.Opacity = 0.6;

        // 清空按钮面板并动态添加新按钮
        if (ActionDialogButtonsPanel != null)
        {
            ActionDialogButtonsPanel.Children.Clear();
            for (int i = 0; i < buttons.Length; i++)
            {
                int index = i;
                var btn = new Button.Button
                {
                    Content = buttons[i],
                    Width = 100,
                    Margin = new Thickness(i > 0 ? 10 : 0, 0, 0, 0)
                };
                if (i == 0)
                {
                    btn.Background = this.FindResource("ThemeBrush") as IBrush ?? Brushes.Transparent;
                    btn.Foreground = Brushes.White;
                }
                btn.Click += (s, e) =>
                {
                    ActionDialogResult = index;
                    HideActionDialog();
                };
                ActionDialogButtonsPanel.Children.Add(btn);
            }
        }

        return Task.Run(async () =>
        {
            while (IsShowActionDialog) await Task.Delay(10);

            return ActionDialogResult;
        });
    }

    private void ToastGrid_MouseLeftButtonDown(object? sender, PointerPressedEventArgs e)
    {
        IsShowToast = false;
        _toastCancellationTokenSource?.Cancel();
        ToastGrid.PointerPressed -= ToastGrid_MouseLeftButtonDown;
    }

    public Task<string?> ShowInputModalAsync(string title, string message, string value, Func<string, bool> validate)
    {
        IsShowInputModal = true;
        ToastGrid.IsVisible = true;
        ToastBorder.IsVisible = false;
        DialogBorder.IsVisible = false;
        InputModalBorder.IsVisible = true;

        DialogMessage = message;
        DialogTitle = title;
        InputModalValue = value;
        InputModalValidFnc = validate;

        InputModalInputBox.Text = InputModalValue;
        InputModalBorder.RenderTransform = TransformOperations.Parse("translateY(10px)");

        Masklayer.Opacity = 0.6;

        return Task.Run(async () =>
        {
            while (IsShowInputModal) await Task.Delay(10);

            if (IsDialogConfirm) return InputValue;

            throw new Exception("Input cancel");
        });
    }


    private void HideToast()
    {
        if (ToastGrid == null)
            return;

        ToastBorder.RenderTransform = TransformOperations.Parse("translateY(-150px)");
        Masklayer.Opacity = 0;
        ToastGrid.IsVisible = false;
    }

    private static void OnIsCanBackChanged(AvaloniaPropertyChangedEventArgs e)
    {
        var that = e.Sender as DefaultWindow;
        if (that != null)
        {
            //if (that.IsCanBack)
            //{
            //    VisualStateManager.GoToState(that, "CanBackState", true);
            //}
            //else
            //{
            //    VisualStateManager.GoToState(that, "Normal", true);
            //}
        }
    }

    private static void OnPageContainerChanged(AvaloniaPropertyChangedEventArgs e)
    {
        var that = (DefaultWindow)e.Sender;
        if (that == null) return;

        if (e.OldValue is PageContainer oldPc && that._onLoadPagedHandler != null)
        {
            oldPc.OnLoadPaged -= that._onLoadPagedHandler;
        }

        if (e.NewValue is PageContainer newPc)
        {
            that.IsCanBack = newPc.Index >= 1;

            that._onLoadPagedHandler = (s, v) =>
            {
                var pc = s as PageContainer;
                that.IsCanBack = pc?.Index >= 1;
            };

            newPc.OnLoadPaged += that._onLoadPagedHandler;
        }
        else
        {
            that._onLoadPagedHandler = null;
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        titleBar = e.NameScope.Find<Grid>("TitleBar");
        ToastBorder = e.NameScope.Find<Border>("ToastBorder");
        Masklayer = e.NameScope.Find<Border>("Masklayer");
        ToastGrid = e.NameScope.Find<Grid>("ToastGrid");
        DialogBorder = e.NameScope.Find<Border>("DialogBorder");
        ActionDialogBorder = e.NameScope.Find<Border>("ActionDialogBorder");
        ActionDialogButtonsPanel = e.NameScope.Find<StackPanel>("ActionDialogButtonsPanel");
        CancelBtn = e.NameScope.Find<Button.Button>("CancelBtn");
        ConfirmBtn = e.NameScope.Find<Button.Button>("ConfirmBtn");
        InputModalBorder = e.NameScope.Find<Border>("InputModalBorder");
        InputModalCancelBtn = e.NameScope.Find<Button.Button>("InputModalCancelBtn");
        InputModalConfirmBtn = e.NameScope.Find<Button.Button>("InputModalConfirmBtn");
        InputModalInputBox = e.NameScope.Find<InputBox>("InputModalInputBox");
        _statusDot = e.NameScope.Find<Border>("StatusDot");
        UpdateConnectionStatus();

        if (CancelBtn != null)
            CancelBtn.Click += (e, c) =>
            {
                IsDialogConfirm = false;
                HideDialog();
            };

        if (ConfirmBtn != null)
            ConfirmBtn.Click += (e, c) =>
            {
                IsDialogConfirm = true;
                HideDialog();
            };

        if (InputModalCancelBtn != null)
            InputModalCancelBtn.Click += (e, c) =>
            {
                IsDialogConfirm = false;
                HideInputModal();
            };

        if (InputModalConfirmBtn != null)
            InputModalConfirmBtn.Click += (e, c) =>
            {
                if (InputModalValidFnc != null && !InputModalValidFnc(InputValue)) return;
                IsDialogConfirm = true;
                HideInputModal();
            };

        if (InputModalInputBox != null)
            InputModalInputBox.TextChanged += (e, c) => { InputValue = InputModalInputBox.Text; };

        if (IsShowToast)
            _ = ShowToastAsync();
    }

    private void HideDialog()
    {
        InputModalBorder.RenderTransform = TransformOperations.Parse("translateY(-150px)");
        Masklayer.Opacity = 0;
        ToastGrid.IsVisible = false;
        IsShowConfirmDialog = false;
    }

    private void HideActionDialog()
    {
        ActionDialogBorder.RenderTransform = TransformOperations.Parse("translateY(-150px)");
        Masklayer.Opacity = 0;
        ToastGrid.IsVisible = false;
        IsShowActionDialog = false;
    }

    private void HideInputModal()
    {
        InputModalBorder.RenderTransform = TransformOperations.Parse("translateY(-150px)");
        Masklayer.Opacity = 0;
        ToastGrid.IsVisible = false;
        IsShowInputModal = false;
    }

    private void UpdateConnectionStatus()
    {
        if (_statusDot == null) return;

        IBrush brush;
        string toolTipKey;

        switch (ConnectionStatus)
        {
            case ConnectionStatus.Connected:
                brush = new SolidColorBrush(Color.Parse("#24bf5f"));
                toolTipKey = "ConnectionStatusConnected";
                break;
            case ConnectionStatus.Disconnected:
                brush = new SolidColorBrush(Color.Parse("#f51837"));
                toolTipKey = "ConnectionStatusDisconnected";
                break;
            case ConnectionStatus.Checking:
            default:
                brush = new SolidColorBrush(Color.Parse("#999999"));
                toolTipKey = "ConnectionStatusChecking";
                break;
        }

        _statusDot.Background = brush;
        ToolTip.SetTip(_statusDot, Application.Current?.FindResource(toolTipKey) ?? string.Empty);
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        IsWindowClosed = true;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (titleBar == null) return;

        try
        {
            var point = e.GetCurrentPoint(this);
            if (titleBar.Bounds.Contains(point.Position) && point.Properties.IsLeftButtonPressed)
            {
                BeginMoveDrag(e);
            }
        }
        catch
        {
            // ignored
        }
    }


    #region sys command

    public static ReactiveCommand<Unit, Unit> MinimizeWindowCommand { get; private set; }
    public static ReactiveCommand<Unit, Unit> RestoreWindowCommand { get; private set; }
    public static ReactiveCommand<Unit, Unit> MaximizeWindowCommand { get; private set; }
    public static ReactiveCommand<Unit, Unit> CloseWindowCommand { get; private set; }
    public static ReactiveCommand<Unit, Unit> LogoButtonClickCommand { get; private set; }
    public static ReactiveCommand<Unit, Unit> BackCommand { get; private set; }

    #endregion
}

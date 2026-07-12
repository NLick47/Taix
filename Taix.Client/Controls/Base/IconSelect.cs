using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using ReactiveUI;

namespace Taix.Client.Controls.Base;

public class IconSelect : TemplatedControl
{
    public static readonly DirectProperty<IconSelect, List<string>> IconsProperty =
        AvaloniaProperty.RegisterDirect<IconSelect, List<string>>(
            nameof(Icons),
            o => o.Icons,
            (o, v) => o.Icons = v);

    public static readonly DirectProperty<IconSelect, string> URLProperty =
        AvaloniaProperty.RegisterDirect<IconSelect, string>(
            nameof(URL),
            o => o.URL,
            (o, v) => o.URL = v);

    public static readonly DirectProperty<IconSelect, bool> IsOpenProperty =
        AvaloniaProperty.RegisterDirect<IconSelect, bool>(
            nameof(IsOpen),
            o => o.IsOpen,
            (o, v) => o.IsOpen = v);

    private List<string> _icons = new();
    private bool _isOpen;
    private string _url = string.Empty;
    private Popup? _popup;
    private Avalonia.Controls.Window? _attachedWindow;
    private IDisposable? _isOpenSubscription;

    public IconSelect()
    {
        Focusable = true;
        URL = "avares://Taix/Resources/Emoji/(1).png";
        ShowSelectCommand = ReactiveCommand.Create(OnToggleSelect);
        FileSelectCommand = ReactiveCommand.CreateFromTask(OnFileSelect);
        LoadIcons();
    }

    public List<string> Icons
    {
        get => _icons;
        set => SetAndRaise(IconsProperty, ref _icons, value);
    }

    public string URL
    {
        get => _url;
        set => SetAndRaise(URLProperty, ref _url, value);
    }

    public bool IsOpen
    {
        get => _isOpen;
        set => SetAndRaise(IsOpenProperty, ref _isOpen, value);
    }

    protected override Type StyleKeyOverride => typeof(IconSelect);

    public ICommand ShowSelectCommand { get; private set; }
    public ICommand FileSelectCommand { get; private set; }

    private void LoadIcons()
    {
        var list = new List<string>();
        for (var i = 1; i < 45; i++)
            list.Add($"avares://Taix/Resources/Emoji/({i}).png");
        Icons = list;
    }

    private void OnToggleSelect()
    {
        IsOpen = !IsOpen;
    }

    private async Task OnFileSelect()
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider is not { } storage)
            return;

        var result = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Images")
                {
                    Patterns = ["*.png", "*.jpg", "*.jpeg"]
                }
            ]
        });

        if (result is { Count: > 0 } && result[0].TryGetLocalPath() is { } path)
        {
            URL = path;
            IsOpen = false;
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        DetachPopupEvents();
        _popup = e.NameScope.Find<Popup>("Popup");
        AttachPopupEvents();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _isOpenSubscription?.Dispose();
        _isOpenSubscription = this.GetObservable(IsOpenProperty).Subscribe(OnIsOpenChanged);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _isOpenSubscription?.Dispose();
        _isOpenSubscription = null;
        DetachPopupEvents();
        DetachTopLevelEvents();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape && IsOpen)
        {
            IsOpen = false;
            e.Handled = true;
        }

        base.OnKeyDown(e);
    }

    private void AttachPopupEvents()
    {
        if (_popup == null) return;
        _popup.Closed += OnPopupClosed;
    }

    private void DetachPopupEvents()
    {
        if (_popup == null) return;
        _popup.Closed -= OnPopupClosed;
    }

    private void OnPopupClosed(object? sender, EventArgs e)
    {
        IsOpen = false;
    }

    private void OnIsOpenChanged(bool isOpen)
    {
        if (isOpen)
            AttachTopLevelEvents();
        else
            DetachTopLevelEvents();
    }

    private void AttachTopLevelEvents()
    {
        DetachTopLevelEvents();

        _attachedWindow = TopLevel.GetTopLevel(this) as Avalonia.Controls.Window;
        if (_attachedWindow == null) return;

        _attachedWindow.AddHandler(
            InputElement.PointerPressedEvent,
            OnTopLevelPointerPressed,
            RoutingStrategies.Tunnel);
    }

    private void DetachTopLevelEvents()
    {
        if (_attachedWindow == null) return;

        _attachedWindow.RemoveHandler(
            InputElement.PointerPressedEvent,
            OnTopLevelPointerPressed);
        _attachedWindow = null;
    }

    private void OnTopLevelPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var source = e.Source as Visual;
        if (source == null) return;

        if (IsDescendantOf(source, _popup?.Child))
            return;

        if (IsDescendantOf(source, this))
            return;

        IsOpen = false;
    }

    private static bool IsDescendantOf(Visual? node, Visual? ancestor)
    {
        if (node == null || ancestor == null) return false;
        if (node == ancestor) return true;

        var current = node.GetVisualParent();
        while (current != null)
        {
            if (current == ancestor)
                return true;
            current = current.GetVisualParent();
        }

        return false;
    }
}

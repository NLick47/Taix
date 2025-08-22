using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using ReactiveUI;

namespace UI.Controls.Base;

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

    private string _url;

    private Border SelectContainer;

    public IconSelect()
    {
        this.GetObservable(IsOpenProperty).Subscribe(isOpen => { HandleWindowEvents(isOpen); });
        URL = "avares://Taix/Resources/Emoji/(1).png";
        ShowSelectCommand = ReactiveCommand.Create<object>(OnShowSelect);
        FileSelectCommand = ReactiveCommand.CreateFromTask<object>(OnFileSelect);
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
        for (var i = 1; i < 45; i++) list.Add($"avares://Taix/Resources/Emoji/({i}).png");
        Icons = list;
    }

    private void Reset()
    {
        URL = Icons[0];
    }

    private void OnShowSelect(object obj)
    {
        IsOpen = true;
    }


    private async Task OnFileSelect(object obj)
    {
        var storage = TopLevel.GetTopLevel(this).StorageProvider;
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
        if (result?.Count > 0) URL = result[0].Path.LocalPath;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        SelectContainer = e.NameScope.Get<Border>("SelectContainer");
    }

    private void HandleWindowEvents(bool isOpen)
    {
        var window = this.GetVisualRoot() as Avalonia.Controls.Window;
        if (window != null)
        {
            if (isOpen)
            {
                window.PointerPressed += OnWindowPointerPressed;
                window.Deactivated += OnDeactivated;
            }
            else
            {
                window.PointerPressed -= OnWindowPointerPressed;
                window.Deactivated -= OnDeactivated;
            }
        }
    }

    private void OnDeactivated(object sender, EventArgs e)
    {
        IsOpen = false;
    }

    private void OnWindowPointerPressed(object sender, PointerPressedEventArgs e)
    {
        IsOpen = false;
    }

    private bool IsInControl(PointerEventArgs e)
    {
        var p = e.GetPosition(SelectContainer);
        if (p.X < 0 || p.Y < 0 || p.X > SelectContainer.Bounds.Width || p.Y > SelectContainer.Bounds.Height)
            return false;
        return true;
    }
}
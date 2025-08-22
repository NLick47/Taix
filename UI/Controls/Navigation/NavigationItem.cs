using System;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using UI.Base.Color;
using UI.Controls.Base;
using Colors = Avalonia.Media.Colors;

namespace UI.Controls.Navigation;

public class NavigationItem : TemplatedControl
{
    public delegate void NavigationEventHandler(object sender, PointerPressedEventArgs e);

    public static readonly DirectProperty<NavigationItem, int> IDProperty =
        AvaloniaProperty.RegisterDirect<NavigationItem, int>(
            nameof(ID),
            o => o.ID,
            (o, v) => o.ID = v);

    public static readonly DirectProperty<NavigationItem, ICommand> CommandProperty =
        AvaloniaProperty.RegisterDirect<NavigationItem, ICommand>(
            nameof(Command),
            o => o.Command,
            (o, v) => o.Command = v);

    public static readonly DirectProperty<NavigationItem, object> CommandParameterProperty =
        AvaloniaProperty.RegisterDirect<NavigationItem, object>(
            nameof(CommandParameter),
            o => o.CommandParameter,
            (o, v) => o.CommandParameter = v);

    public static readonly StyledProperty<IconTypes> IconProperty =
        AvaloniaProperty.Register<NavigationItem, IconTypes>(nameof(Icon));

    public static readonly StyledProperty<IconTypes> SelectedIconProperty =
        AvaloniaProperty.Register<NavigationItem, IconTypes>(nameof(SelectedIcon));

    public static readonly StyledProperty<ColorTypes> IconColorProperty =
        AvaloniaProperty.Register<NavigationItem, ColorTypes>(nameof(IconColor), ColorTypes.Blue);

    public static readonly StyledProperty<SolidColorBrush> IconColorBrushProperty =
        AvaloniaProperty.Register<NavigationItem, SolidColorBrush>(nameof(IconColorBrush),
            new SolidColorBrush(Colors.Blue));

    public static readonly DirectProperty<NavigationItem, string> TitleProperty =
        AvaloniaProperty.RegisterDirect<NavigationItem, string>(
            nameof(Title),
            o => o.Title,
            (o, v) => o.Title = v);

    public static readonly DirectProperty<NavigationItem, string> BadgeTextProperty =
        AvaloniaProperty.RegisterDirect<NavigationItem, string>(
            nameof(BadgeText),
            o => o.BadgeText,
            (o, v) => o.BadgeText = v);

    public static readonly DirectProperty<NavigationItem, string> UriProperty =
        AvaloniaProperty.RegisterDirect<NavigationItem, string>(
            nameof(Uri),
            o => o.Uri,
            (o, v) => o.Uri = v);

    public static readonly DirectProperty<NavigationItem, bool> IsSelectedProperty =
        AvaloniaProperty.RegisterDirect<NavigationItem, bool>(
            nameof(IsSelected),
            o => o.IsSelected,
            (o, v) => o.IsSelected = v);

    private static NavigationItem _currentPressedItem;

    private string _badgeText = string.Empty;

    private ICommand _command;

    private object _commandParameter;
    private int _id;

    private bool _isSelected;

    private string _title = string.Empty;

    private string _uri = string.Empty;

    public NavigationItem()
    {
        PointerPressed += OnPointerPressed;
    }

    public int ID
    {
        get => _id;
        set => SetAndRaise(IDProperty, ref _id, value);
    }

    public ICommand Command
    {
        get => _command;
        set => SetAndRaise(CommandProperty, ref _command, value);
    }

    public object CommandParameter
    {
        get => _commandParameter;
        set => SetAndRaise(CommandParameterProperty, ref _commandParameter, value);
    }

    public IconTypes Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public IconTypes SelectedIcon
    {
        get => GetValue(SelectedIconProperty);
        set => SetValue(SelectedIconProperty, value);
    }

    public ColorTypes IconColor
    {
        get => GetValue(IconColorProperty);
        set => SetValue(IconColorProperty, value);
    }

    public SolidColorBrush IconColorBrush
    {
        get => GetValue(IconColorBrushProperty);
        set => SetValue(IconColorBrushProperty, value);
    }

    public string Title
    {
        get => _title;
        set => SetAndRaise(TitleProperty, ref _title, value);
    }

    public string BadgeText
    {
        get => _badgeText;
        set => SetAndRaise(BadgeTextProperty, ref _badgeText, value);
    }

    public string Uri
    {
        get => _uri;
        set => SetAndRaise(UriProperty, ref _uri, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetAndRaise(IsSelectedProperty, ref _isSelected, value);
    }

    protected override Type StyleKeyOverride => typeof(NavigationItem);
    public event NavigationEventHandler MouseUp;

    private void OnPointerPressed(object sender, PointerPressedEventArgs e)
    {
        MouseUp?.Invoke(this, e);
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) Command?.Execute(CommandParameter);
    }
}
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using DynamicData.Binding;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using UI.Base.Color;
using UI.Controls.Base;

namespace UI.Controls.Navigation
{
    public class NavigationItem : TemplatedControl
    {
        private int _id;
        public static readonly DirectProperty<NavigationItem, int> IDProperty = 
            AvaloniaProperty.RegisterDirect<NavigationItem, int>(
                nameof(ID),
                o => o.ID,
                (o, v) => o.ID = v);
        public int ID
        {
            get => _id;
            set => SetAndRaise(IDProperty, ref _id, value);
        }

        private ICommand _command;
        public static readonly DirectProperty<NavigationItem, ICommand> CommandProperty =
            AvaloniaProperty.RegisterDirect<NavigationItem, ICommand>(
                nameof(Command),
                o => o.Command,
                (o, v) => o.Command = v);
        public ICommand Command
        {
            get => _command;
            set => SetAndRaise(CommandProperty, ref _command, value);
        }

        private object _commandParameter;
        public static readonly DirectProperty<NavigationItem, object> CommandParameterProperty =
            AvaloniaProperty.RegisterDirect<NavigationItem, object>(
                nameof(CommandParameter),
                o => o.CommandParameter,
                (o, v) => o.CommandParameter = v);
        public object CommandParameter
        {
            get => _commandParameter;
            set => SetAndRaise(CommandParameterProperty, ref _commandParameter, value);
        }

        public static readonly StyledProperty<IconTypes> IconProperty =
            AvaloniaProperty.Register<NavigationItem, IconTypes>(nameof(Icon), IconTypes.None);

        public IconTypes Icon
        {
            get => GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }

        public static readonly StyledProperty<IconTypes> SelectedIconProperty =
            AvaloniaProperty.Register<NavigationItem, IconTypes>(nameof(SelectedIcon), IconTypes.None);

        public IconTypes SelectedIcon
        {
            get => GetValue(SelectedIconProperty);
            set => SetValue(SelectedIconProperty, value);
        }

        public static readonly StyledProperty<ColorTypes> IconColorProperty =
            AvaloniaProperty.Register<NavigationItem, ColorTypes>(nameof(IconColor), ColorTypes.Blue);

        public ColorTypes IconColor
        {
            get => GetValue(IconColorProperty);
            set => SetValue(IconColorProperty, value);
        }

        public static readonly StyledProperty<SolidColorBrush> IconColorBrushProperty =
            AvaloniaProperty.Register<NavigationItem, SolidColorBrush>(nameof(IconColorBrush),
                new SolidColorBrush(Avalonia.Media.Colors.Blue));

        public SolidColorBrush IconColorBrush
        {
            get => GetValue(IconColorBrushProperty);
            set => SetValue(IconColorBrushProperty, value);
        }

        private string _title = string.Empty;
        public static readonly DirectProperty<NavigationItem, string> TitleProperty =
            AvaloniaProperty.RegisterDirect<NavigationItem, string>(
                nameof(Title),
                o => o.Title,
                (o, v) => o.Title = v);
        public string Title
        {
            get => _title;
            set => SetAndRaise(TitleProperty, ref _title, value);
        }

        private string _badgeText = string.Empty;
        public static readonly DirectProperty<NavigationItem, string> BadgeTextProperty =
            AvaloniaProperty.RegisterDirect<NavigationItem, string>(
                nameof(BadgeText),
                o => o.BadgeText,
                (o, v) => o.BadgeText = v);
        public string BadgeText
        {
            get => _badgeText;
            set => SetAndRaise(BadgeTextProperty, ref _badgeText, value);
        }

        private string _uri = string.Empty;
        public static readonly DirectProperty<NavigationItem, string> UriProperty =
            AvaloniaProperty.RegisterDirect<NavigationItem, string>(
                nameof(Uri),
                o => o.Uri,
                (o, v) => o.Uri = v);
        public string Uri
        {
            get => _uri;
            set => SetAndRaise(UriProperty, ref _uri, value);
        }

        private bool _isSelected = false;
        public static readonly DirectProperty<NavigationItem, bool> IsSelectedProperty =
            AvaloniaProperty.RegisterDirect<NavigationItem, bool>(
                nameof(IsSelected),
                o => o.IsSelected,
                (o, v) => o.IsSelected = v);
        public bool IsSelected
        {
            get => _isSelected;
            set => SetAndRaise(IsSelectedProperty, ref _isSelected, value);
        }
        private static NavigationItem _currentPressedItem;

        public delegate void NavigationEventHandler(object sender, PointerPressedEventArgs e);
        public event NavigationEventHandler MouseUp;

        public NavigationItem()
        {
            this.PointerPressed += OnPointerPressed;
        }

        protected override Type StyleKeyOverride => typeof(NavigationItem);

        private void OnPointerPressed(object sender, PointerPressedEventArgs e)
        {
            
            MouseUp?.Invoke(this, e);
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                Command?.Execute(CommandParameter);
            }
        }
    }
}

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
        public static readonly StyledProperty<int> IDProperty =
        AvaloniaProperty.Register<NavigationItem, int>(nameof(ID));

        public int ID
        {
            get => GetValue(IDProperty);
            set => SetValue(IDProperty, value);
        }

        public static readonly StyledProperty<ICommand> CommandProperty =
            AvaloniaProperty.Register<NavigationItem, ICommand>(nameof(Command));

        public ICommand Command
        {
            get => GetValue(CommandProperty);
            set => SetValue(CommandProperty, value);
        }

        public static readonly StyledProperty<object> CommandParameterProperty =
            AvaloniaProperty.Register<NavigationItem, object>(nameof(CommandParameter));

        public object CommandParameter
        {
            get => GetValue(CommandParameterProperty);
            set => SetValue(CommandParameterProperty, value);
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

        public static readonly StyledProperty<string> TitleProperty =
            AvaloniaProperty.Register<NavigationItem, string>(nameof(Title), string.Empty);

        public string Title
        {
            get => GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public static readonly StyledProperty<string> BadgeTextProperty =
            AvaloniaProperty.Register<NavigationItem, string>(nameof(BadgeText), string.Empty);

        public string BadgeText
        {
            get => GetValue(BadgeTextProperty);
            set => SetValue(BadgeTextProperty, value);
        }

        public static readonly StyledProperty<string> UriProperty =
            AvaloniaProperty.Register<NavigationItem, string>(nameof(Uri), string.Empty);

        public string Uri
        {
            get => GetValue(UriProperty);
            set => SetValue(UriProperty, value);
        }

        public static readonly StyledProperty<bool> IsSelectedProperty =
            AvaloniaProperty.Register<NavigationItem, bool>(nameof(IsSelected), false);

        public bool IsSelected
        {
            get => GetValue(IsSelectedProperty);
            set => SetValue(IsSelectedProperty, value);
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

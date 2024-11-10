using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using DynamicData.Binding;
using ReactiveUI;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using UI.Controls.Button;

namespace UI.Controls.Window
{
    public class DefaultWindow : Avalonia.Controls.Window
    {
        public  static readonly StyledProperty<IImage?> IconSourceProperty =
    AvaloniaProperty.Register<DefaultWindow, IImage?>(nameof(IconSource));

        public static readonly StyledProperty<bool> MaximizeVisibleProperty =
            AvaloniaProperty.Register<DefaultWindow, bool>(nameof(MaximizeVisible));

        public static readonly StyledProperty<bool> RestoreVisibleProperty =
            AvaloniaProperty.Register<DefaultWindow, bool>(nameof(RestoreVisible));

        public bool MaximizeVisible { get => GetValue(MaximizeVisibleProperty); set => SetValue(MaximizeVisibleProperty,value); }
       

        public bool RestoreVisible { get => GetValue(RestoreVisibleProperty); set => SetValue(RestoreVisibleProperty, value); }


        #region sys command
        public static ReactiveCommand<Unit, Unit> MinimizeWindowCommand { get; private set; }
        public static ReactiveCommand<Unit, Unit> RestoreWindowCommand { get; private set; }
        public static ReactiveCommand<Unit, Unit> MaximizeWindowCommand { get; private set; }
        public static ReactiveCommand<Unit, Unit> CloseWindowCommand { get; private set; }
        public static ReactiveCommand<Unit, Unit> LogoButtonClickCommand { get; private set; }
        public static ReactiveCommand<Unit, Unit> BackCommand { get; private set; }
        #endregion
        public IImage? IconSource
        {
            get => GetValue(IconSourceProperty);
            set => SetValue(IconSourceProperty, value);
        }
        public DefaultWindow() {
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

            MinimizeWindowCommand = ReactiveCommand.Create(() =>
            {
                WindowState = WindowState.Minimized;
            });

            RestoreWindowCommand = ReactiveCommand.Create(() =>
            {
                WindowState = WindowState.Normal;
            });

            MaximizeWindowCommand = ReactiveCommand.Create(() =>
            {
                WindowState = WindowState.Maximized;
            });

            CloseWindowCommand = ReactiveCommand.Create(() =>
            {
                this.Hide();
            });
        }


        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                this.BeginMoveDrag(e);
            }
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);
            Icon = IconSource is Bitmap bitmap ? new WindowIcon(bitmap) : default;
        }

        protected override Type StyleKeyOverride => typeof(DefaultWindow);

    }
}

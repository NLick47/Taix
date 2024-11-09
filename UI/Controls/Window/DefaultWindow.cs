using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UI.Controls.Button;

namespace UI.Controls.Window
{
    public class DefaultWindow : Avalonia.Controls.Window
    {
        public  static readonly StyledProperty<IImage?> IconSourceProperty =
    AvaloniaProperty.Register<DefaultWindow, IImage?>(nameof(IconSource));

        public IImage? IconSource
        {
            get => GetValue(IconSourceProperty);
            set => SetValue(IconSourceProperty, value);
        }
        public DefaultWindow() {
          
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
            Icon = new WindowIcon(IconSource as Bitmap);
        }

        protected override Type StyleKeyOverride => typeof(DefaultWindow);
    }
}

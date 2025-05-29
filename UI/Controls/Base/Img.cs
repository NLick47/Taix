using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UI.Controls.Base
{
    public class Img : TemplatedControl
    {
        public CornerRadius Radius
        {
            get { return GetValue(RadiusProperty); }
            set { SetValue(RadiusProperty, value); }
        }

        public static readonly StyledProperty<CornerRadius> RadiusProperty =
          AvaloniaProperty.Register<Img, CornerRadius>(nameof(Radius));

        public IImage Resource
        {
            get { return GetValue(ResourceProperty); }
            set { SetValue(ResourceProperty, value); }
        }
        public static readonly StyledProperty<IImage> ResourceProperty =
         AvaloniaProperty.Register<Img, IImage>(nameof(Resource), new Bitmap(AssetLoader.Open(new Uri("avares://Taix/Resources/Icons/defaultIcon.png"))));
        
        private string _url;
        public string URL
        {
            get => _url;
            set => SetAndRaise(URLProperty, ref _url, value);
        }
        public static readonly DirectProperty<Img, string> URLProperty =
            AvaloniaProperty.RegisterDirect<Img, string>(
                nameof(URL),
                o => o.URL,
                (o, v) => o.URL = v);


        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            if (e.Property == URLProperty && e.OldValue != e.NewValue && e.NewValue != null)
            {
                var control = (e.Sender as Img);
                control.Handle(e.NewValue.ToString());
            }
        }

     

        private async void Handle(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return;
            }
            if (path.IndexOf("avares:") != -1)
            {
                Resource = new Bitmap(AssetLoader.Open(new Uri(path)));
                return;
            }
            try
            {

                string src = path.IndexOf(":") != -1 ? path : System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path);
                var desktop = Application.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
                var storage = desktop.MainWindow.StorageProvider;
                var result = await storage.TryGetFileFromPathAsync(src);
                if (result != null)
                {
                    Resource = new Bitmap(await result.OpenReadAsync());
                }
            }
            catch (Exception e)
            {

                
            }
        }

        protected override Type StyleKeyOverride => typeof(Img);
    }
}

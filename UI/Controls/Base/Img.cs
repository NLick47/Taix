using System;
using System.IO;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;

namespace UI.Controls.Base;

public class Img : TemplatedControl
{
    public static readonly StyledProperty<CornerRadius> RadiusProperty =
        AvaloniaProperty.Register<Img, CornerRadius>(nameof(Radius));

    public static readonly StyledProperty<IImage> ResourceProperty =
        AvaloniaProperty.Register<Img, IImage>(nameof(Resource),
            new Bitmap(AssetLoader.Open(new Uri("avares://Taix/Resources/Icons/defaultIcon.png"))));

    public static readonly DirectProperty<Img, string> URLProperty =
        AvaloniaProperty.RegisterDirect<Img, string>(
            nameof(URL),
            o => o.URL,
            (o, v) => o.URL = v);

    private string _url;

    public CornerRadius Radius
    {
        get => GetValue(RadiusProperty);
        set => SetValue(RadiusProperty, value);
    }

    public IImage Resource
    {
        get => GetValue(ResourceProperty);
        set => SetValue(ResourceProperty, value);
    }

    public string URL
    {
        get => _url;
        set => SetAndRaise(URLProperty, ref _url, value);
    }

    protected override Type StyleKeyOverride => typeof(Img);


    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == URLProperty && e.OldValue != e.NewValue && e.NewValue != null)
        {
            var control = e.Sender as Img;
            control.Handle(e.NewValue.ToString());
        }
    }


    private async void Handle(string path)
    {
        if (string.IsNullOrEmpty(path)) return;
        
        try
        {
            if (path.IndexOf("avares:", StringComparison.Ordinal) != -1)
            {
                Resource = new Bitmap(AssetLoader.Open(new Uri(path)));
                return;
            }
            var src = path.IndexOf(":", StringComparison.Ordinal) != -1 ? path : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path);
            var desktop = Application.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
            var storage = desktop.MainWindow.StorageProvider;
            var result = await storage.TryGetFileFromPathAsync(src);
            if (result != null) Resource = new Bitmap(await result.OpenReadAsync());
        }
        catch (Exception e)
        {
            // ignored
        }
    }
}
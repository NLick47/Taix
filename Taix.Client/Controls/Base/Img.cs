using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Taix.Client.Librarys.Image;
using Taix.Client.Logging;

namespace Taix.Client.Controls.Base;

public class Img : TemplatedControl
{
    public static readonly StyledProperty<CornerRadius> RadiusProperty =
        AvaloniaProperty.Register<Img, CornerRadius>(nameof(Radius));

    public static readonly StyledProperty<IImage> ResourceProperty =
        AvaloniaProperty.Register<Img, IImage>(nameof(Resource));

    public static readonly DirectProperty<Img, string> URLProperty =
        AvaloniaProperty.RegisterDirect<Img, string>(
            nameof(URL),
            o => o.URL,
            (o, v) => o.URL = v);

    private string _url;
    private CancellationTokenSource? _cts;
    private Bitmap? _pendingRelease;

    public Img()
    {
        Resource = Imager.GetDefaultBitmap();
    }

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

    private void ReloadFromUrl()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        var path = URL;

        if (_pendingRelease != null)
        {
            Imager.Release(_pendingRelease);
            _pendingRelease = null;
        }

        if (Resource is Bitmap oldBmp)
            Imager.Release(oldBmp);

        if (string.IsNullOrWhiteSpace(path))
        {
            Resource = Imager.GetDefaultBitmap();
            return;
        }

        if (Imager.TryGetFromCache(path, out var cached))
        {
            Resource = cached;
            return;
        }

        Resource = Imager.GetDefaultBitmap();

        if (Imager.IsFailed(path))
            return;

        _ = LoadImageAsync(path, token);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (e.Property != URLProperty) return;
        if (e.OldValue == e.NewValue) return;

        ReloadFromUrl();
    }

    private async Task LoadImageAsync(string path, CancellationToken token)
    {
        Bitmap? bitmap = null;
        try
        {
            bitmap = await Imager.LoadAsync(path);
            token.ThrowIfCancellationRequested();

            if (URL != path)
                return;

            if (Resource is Bitmap oldBmp && oldBmp != bitmap)
                Imager.Release(oldBmp);
            Resource = bitmap;
            bitmap = null;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Logger.Error($"图片加载失败：{path}", ex);
        }
        finally
        {
            if (bitmap != null)
                Imager.Release(bitmap);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;

        if (Resource is Bitmap bmp)
        {
            _pendingRelease = bmp;
            Resource = null;
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        if (_pendingRelease != null)
        {
            Resource = _pendingRelease;
            _pendingRelease = null;
            return;
        }

        if (Resource == null)
            ReloadFromUrl();
    }
}

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

    public static readonly StyledProperty<int> DecodeWidthProperty =
        AvaloniaProperty.Register<Img, int>(nameof(DecodeWidth));

    public static readonly StyledProperty<int> DecodeHeightProperty =
        AvaloniaProperty.Register<Img, int>(nameof(DecodeHeight));

    public static readonly DirectProperty<Img, string> URLProperty =
        AvaloniaProperty.RegisterDirect<Img, string>(
            nameof(URL),
            o => o.URL,
            (o, v) => o.URL = v);

    private string _url = string.Empty;
    private CancellationTokenSource? _cts;
    private string? _loadingUrl;

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

    public int DecodeWidth
    {
        get => GetValue(DecodeWidthProperty);
        set => SetValue(DecodeWidthProperty, value);
    }

    public int DecodeHeight
    {
        get => GetValue(DecodeHeightProperty);
        set => SetValue(DecodeHeightProperty, value);
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

        if (e.Property == URLProperty && !Equals(e.OldValue, e.NewValue))
            ReloadFromUrl();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        if (!string.IsNullOrWhiteSpace(URL) && Resource == Imager.GetDefaultBitmap())
            ReloadFromUrl();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        CancelLoading();
        Resource = Imager.GetDefaultBitmap();
    }

    private void ReloadFromUrl()
    {
        var path = URL;

        if (_loadingUrl == path && _cts != null && !_cts.IsCancellationRequested)
            return;

        CancelLoading();

        if (string.IsNullOrWhiteSpace(path))
        {
            Resource = Imager.GetDefaultBitmap();
            return;
        }

        var decodeWidth = DecodeWidth;
        var decodeHeight = DecodeHeight;

        if (Imager.TryGetFromCache(path, out var cached, decodeWidth, decodeHeight))
        {
            Resource = cached ?? Imager.GetDefaultBitmap();
            return;
        }

        if (Imager.IsFailed(path, decodeWidth, decodeHeight))
        {
            Resource = Imager.GetDefaultBitmap();
            return;
        }

        Resource = Imager.GetDefaultBitmap();

        _cts = new CancellationTokenSource();
        _loadingUrl = path;
        _ = LoadAsync(path, _cts.Token, decodeWidth, decodeHeight);
    }

    private void CancelLoading()
    {
        _loadingUrl = null;
        if (_cts == null) return;
        _cts.Cancel();
        _cts.Dispose();
        _cts = null;
    }

    private async Task LoadAsync(string path, CancellationToken token, int decodeWidth, int decodeHeight)
    {
        try
        {
            var bitmap = await Imager.LoadAsync(
                path,
                decodeWidth: decodeWidth,
                decodeHeight: decodeHeight,
                cancellationToken: token);

            token.ThrowIfCancellationRequested();

            if (URL != path)
                return;

            Resource = bitmap;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Logger.Error($"[Img] 加载失败: {path}", ex);
        }
        finally
        {
            if (_loadingUrl == path)
                _loadingUrl = null;
        }
    }
}

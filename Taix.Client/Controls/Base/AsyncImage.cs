using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Taix.Client.Librarys.Image;
using Taix.Client.Logging;

namespace Taix.Client.Controls.Base;

public class AsyncImage : Image
{
    public static readonly StyledProperty<string> AsyncSourceProperty =
        AvaloniaProperty.Register<AsyncImage, string>(nameof(AsyncSource));

    public string AsyncSource
    {
        get => GetValue(AsyncSourceProperty);
        set => SetValue(AsyncSourceProperty, value);
    }

    private CancellationTokenSource? _cts;
    private Bitmap? _pendingRelease;

    static AsyncImage()
    {
        AsyncSourceProperty.Changed.AddClassHandler<AsyncImage>((img, e) => img.OnAsyncSourceChanged());
    }

    private void OnAsyncSourceChanged()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        var path = AsyncSource;

        if (_pendingRelease != null)
        {
            Imager.Release(_pendingRelease);
            _pendingRelease = null;
        }

        if (Source is Bitmap oldBmp)
            Imager.Release(oldBmp);

        if (string.IsNullOrWhiteSpace(path))
        {
            Source = null;
            return;
        }

        if (Imager.TryGetFromCache(path, out var cached))
        {
            Source = cached;
            return;
        }

        Source = Imager.GetDefaultBitmap();

        if (Imager.IsFailed(path))
            return;

        _ = LoadAsync(path, token);
    }

    private async Task LoadAsync(string path, CancellationToken token)
    {
        Bitmap? bitmap = null;
        try
        {
            bitmap = await Imager.LoadAsync(path);
            token.ThrowIfCancellationRequested();

            if (AsyncSource == path)
            {
                if (Source is Bitmap oldBmp && oldBmp != bitmap)
                    Imager.Release(oldBmp);
                Source = bitmap;
                bitmap = null;
            }
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

        if (Source is Bitmap bmp)
        {
            _pendingRelease = bmp;
            Source = null;
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        if (_pendingRelease != null)
        {
            Source = _pendingRelease;
            _pendingRelease = null;
            return;
        }

        if (Source == null)
            OnAsyncSourceChanged();
    }
}

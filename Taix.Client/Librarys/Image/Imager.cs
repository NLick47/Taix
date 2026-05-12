using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Taix.Client.Logging;
using Taix.Client.Shared.Librarys;

namespace Taix.Client.Librarys.Image;

public class Imager
{
    private static volatile Bitmap? _defaultBitmap;
    private static readonly object DefaultBitmapLock = new();
    private const string DefaultAssetPath = "avares://Taix/Resources/Icons/defaultIcon.png";

    private static readonly SimpleLruCache Cache = new(capacity: 50);
    private static readonly SemaphoreSlim DecodeSemaphore = new(4, 4);

    public static bool TryGetFromCache(string filePath, out Bitmap? bitmap, int decodeWidth = 0, int decodeHeight = 0)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            bitmap = GetDefaultBitmap();
            return true;
        }
        return Cache.TryGet(BuildCacheKey(filePath, decodeWidth, decodeHeight), out bitmap);
    }

    public static void Release(Bitmap? bitmap) { }

    public static bool IsFailed(string filePath, int decodeWidth = 0, int decodeHeight = 0)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return false;
        return Cache.IsFailed(BuildCacheKey(filePath, decodeWidth, decodeHeight));
    }

    public static async Task<Bitmap> LoadAsync(string filePath, string? defaultPath = null, int decodeWidth = 0, int decodeHeight = 0, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return GetDefaultBitmap(defaultPath);

        var cacheKey = BuildCacheKey(filePath, decodeWidth, decodeHeight);

        if (Cache.TryGet(cacheKey, out var cachedBitmap))
            return cachedBitmap;

        if (Cache.IsFailed(cacheKey))
            return GetDefaultBitmap(defaultPath);

        var sw = Stopwatch.StartNew();
        Logger.Debug($"[Imager] 开始异步加载: {filePath}, decode=({decodeWidth}x{decodeHeight})");

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var bytes = await Task.Run(() => TryReadFileBytes(filePath), cancellationToken);
            if (bytes == null)
            {
                Logger.Debug($"[Imager] 文件不存在或无法读取: {filePath}");
                Cache.MarkFailed(cacheKey);
                return GetDefaultBitmap(defaultPath);
            }

            Logger.Debug($"[Imager] 文件读取完成 [{sw.ElapsedMilliseconds}ms]: {filePath}, size={bytes.Length} bytes");

            Bitmap bitmap;
            await DecodeSemaphore.WaitAsync(cancellationToken);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                cancellationToken.ThrowIfCancellationRequested();
                bitmap = await Task.Run(() => BytesToBitmap(bytes, decodeWidth, decodeHeight, cancellationToken), cancellationToken);
                Logger.Debug($"[Imager] 解码完成 [{sw.ElapsedMilliseconds}ms]: {filePath}, pixel={bitmap.PixelSize.Width}x{bitmap.PixelSize.Height}");
            }
            finally
            {
                DecodeSemaphore.Release();
            }

            cancellationToken.ThrowIfCancellationRequested();
            var result = Cache.Add(cacheKey, bitmap);
            Logger.Debug($"[Imager] 加载完成 [{sw.ElapsedMilliseconds}ms]: {filePath}");
            return result;
        }
        catch (OperationCanceledException)
        {
            Logger.Debug($"[Imager] 加载取消 [{sw.ElapsedMilliseconds}ms]: {filePath}");
            throw;
        }
        catch (Exception ex)
        {
            Logger.Error($"[Imager] 异步加载图片异常 [{sw.ElapsedMilliseconds}ms]: {filePath}", ex);
            Cache.MarkFailed(cacheKey);
            return GetDefaultBitmap(defaultPath);
        }
    }

    public static Bitmap GetDefaultBitmap(string? defaultPath = null)
    {
        if (!string.IsNullOrWhiteSpace(defaultPath))
        {
            try
            {
                return LoadDefaultBitmapFromPath(defaultPath);
            }
            catch
            {
                // ignored
            }
        }

        if (_defaultBitmap != null)
            return _defaultBitmap;

        lock (DefaultBitmapLock)
        {
            _defaultBitmap ??= LoadDefaultBitmap();
            return _defaultBitmap;
        }
    }

    private static Bitmap LoadDefaultBitmap()
    {
        try
        {
            var uri = new Uri(DefaultAssetPath);
            using var stream = AssetLoader.Open(uri);
            return new Bitmap(stream);
        }
        catch (Exception ex)
        {
            Logger.Error($"默认图片加载失败：{DefaultAssetPath}", ex);
            throw;
        }
    }

    private static Bitmap LoadDefaultBitmapFromPath(string path)
    {
        if (path.StartsWith("avares://", StringComparison.Ordinal))
        {
            var uri = new Uri(path);
            using var stream = AssetLoader.Open(uri);
            return new Bitmap(stream);
        }

        var fullPath = TryResolveFullPath(path);
        if (fullPath == null)
            throw new FileNotFoundException("默认图片文件不存在", path);

        using var fs = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return new Bitmap(fs);
    }

    private static string? TryResolveFullPath(string filePath)
    {
        var fullPath = Path.IsPathRooted(filePath)
            ? filePath
            : Path.Combine(FileHelper.GetRootDirectory(), filePath);

        return File.Exists(fullPath) ? fullPath : null;
    }

    private static byte[]? TryReadFileBytes(string filePath)
    {
        if (filePath.StartsWith("avares://", StringComparison.Ordinal))
        {
            try
            {
                var uri = new Uri(filePath);
                using var stream = AssetLoader.Open(uri);
                using var ms = new MemoryStream();
                stream.CopyTo(ms);
                return ms.ToArray();
            }
            catch
            {
                return null;
            }
        }

        var fullPath = TryResolveFullPath(filePath);
        if (fullPath == null)
            return null;

        try
        {
            return File.ReadAllBytes(fullPath);
        }
        catch
        {
            return null;
        }
    }

    private static Bitmap BytesToBitmap(byte[] bytes, int decodeWidth = 0, int decodeHeight = 0, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var ms = new MemoryStream(bytes);
        return DecodeFromStream(ms, decodeWidth, decodeHeight);
    }

    private static Bitmap DecodeFromStream(Stream stream, int decodeWidth, int decodeHeight)
    {
        if (decodeWidth > 0)
            return Bitmap.DecodeToWidth(stream, decodeWidth);
        if (decodeHeight > 0)
            return Bitmap.DecodeToHeight(stream, decodeHeight);
        return new Bitmap(stream);
    }

    private static string BuildCacheKey(string filePath, int decodeWidth, int decodeHeight)
    {
        if (decodeWidth > 0 && decodeHeight > 0)
            return $"{filePath}#w{decodeWidth}h{decodeHeight}";
        if (decodeWidth > 0)
            return $"{filePath}#w{decodeWidth}";
        if (decodeHeight > 0)
            return $"{filePath}#h{decodeHeight}";
        return filePath;
    }

    internal class SimpleLruCache
    {
        private readonly int _capacity;
        private readonly Dictionary<string, Bitmap> _cache = new();
        private readonly LinkedList<string> _lru = new();
        private readonly ConcurrentDictionary<string, byte> _failed = new();
        private readonly object _lock = new();

        public SimpleLruCache(int capacity)
        {
            _capacity = capacity;
        }

        public bool TryGet(string key, out Bitmap? bitmap)
        {
            bitmap = null;

            if (_failed.ContainsKey(key))
                return false;

            lock (_lock)
            {
                if (_cache.TryGetValue(key, out bitmap))
                {
                    _lru.Remove(key);
                    _lru.AddFirst(key);
                    return true;
                }
            }

            return false;
        }

        public bool IsFailed(string key) => _failed.ContainsKey(key);

        public Bitmap Add(string key, Bitmap bitmap)
        {
            _failed.TryRemove(key, out _);

            lock (_lock)
            {
                if (_cache.TryGetValue(key, out var existing))
                {
                    _lru.Remove(key);
                    _lru.AddFirst(key);
                    bitmap.Dispose();
                    return existing;
                }

                while (_cache.Count >= _capacity)
                {
                    var last = _lru.Last;
                    if (last == null) break;

                    var oldKey = last.Value;
                    _lru.RemoveLast();
                    if (_cache.Remove(oldKey, out var oldBitmap))
                    {
                        oldBitmap?.Dispose();
                    }
                }

                _cache[key] = bitmap;
                _lru.AddFirst(key);
                return bitmap;
            }
        }

        public void MarkFailed(string key)
        {
            _failed[key] = 0;
        }

        public void Clear()
        {
            lock (_lock)
            {
                foreach (var bitmap in _cache.Values)
                {
                    bitmap?.Dispose();
                }
                _cache.Clear();
                _lru.Clear();
            }
            _failed.Clear();
        }
    }
}

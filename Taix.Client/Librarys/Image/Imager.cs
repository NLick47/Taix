using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Taix.Client.Logging;
using Taix.Client.Shared.Librarys;

namespace Taix.Client.Librarys.Image;

public class Imager
{
    private static volatile Bitmap? _defaultBitmap;
    private static volatile ExceptionDispatchInfo? _defaultBitmapException;
    private static readonly object DefaultBitmapLock = new();
    private const string DefaultAssetPath = "avares://Taix/Resources/Icons/defaultIcon.png";

    private static readonly BitmapLruCache Cache = new();

    public static Bitmap Load(string filePath, string? defaultPath = null)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            Logger.Warn("图片路径为空，返回默认图片");
            return GetDefaultBitmap(defaultPath);
        }

        if (Cache.TryGet(filePath, out var cachedBitmap))
            return cachedBitmap;

        if (Cache.IsFailed(filePath))
            return GetDefaultBitmap(defaultPath);

        try
        {
            var bitmap = LoadCore(filePath);
            return Cache.Add(filePath, bitmap);
        }
        catch (Exception ex)
        {
            Logger.Error($"无法加载图片：{filePath}，已回退到默认图片", ex);
            Cache.MarkFailed(filePath);
            return GetDefaultBitmap(defaultPath);
        }
    }

    public static bool TryGetFromCache(string filePath, out Bitmap? bitmap)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            bitmap = GetDefaultBitmap();
            return true;
        }
        return Cache.TryGet(filePath, out bitmap);
    }

    public static void Release(Bitmap? bitmap)
    {
        Cache.Release(bitmap);
    }

    public static bool IsFailed(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return false;
        return Cache.IsFailed(filePath);
    }

    public static void ClearCache()
    {
        Cache.Clear();
    }

    public static async Task<Bitmap> LoadAsync(string filePath, string? defaultPath = null)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return GetDefaultBitmap(defaultPath);

        if (Cache.TryGet(filePath, out var cachedBitmap))
            return cachedBitmap;

        if (Cache.IsFailed(filePath))
            return GetDefaultBitmap(defaultPath);

        byte[]? bytes = null;
        await Task.Run(() =>
        {
            try
            {
                bytes = ReadFileBytes(filePath);
            }
            catch (Exception ex)
            {
                Logger.Error($"无法加载图片：{filePath}，已回退到默认图片", ex);
            }
        });

        if (bytes == null)
        {
            Cache.MarkFailed(filePath);
            return GetDefaultBitmap(defaultPath);
        }

        Bitmap bitmap;
        try
        {
            bitmap = await Dispatcher.UIThread.InvokeAsync(() => BytesToBitmap(bytes));
        }
        catch (Exception ex)
        {
            Logger.Error($"UI线程解码图片失败：{filePath}", ex);
            Cache.MarkFailed(filePath);
            return GetDefaultBitmap(defaultPath);
        }

        return Cache.Add(filePath, bitmap);
    }

    public static Bitmap GetDefaultBitmap(string? defaultPath = null)
    {
        if (string.IsNullOrWhiteSpace(defaultPath))
        {
            if (_defaultBitmap != null)
                return _defaultBitmap;

            if (_defaultBitmapException != null)
                _defaultBitmapException.Throw();

            lock (DefaultBitmapLock)
            {
                if (_defaultBitmap != null)
                    return _defaultBitmap;

                if (_defaultBitmapException != null)
                    _defaultBitmapException.Throw();

                try
                {
                    _defaultBitmap = LoadDefaultBitmap();
                    return _defaultBitmap;
                }
                catch (Exception ex)
                {
                    _defaultBitmapException = ExceptionDispatchInfo.Capture(ex);
                    throw;
                }
            }
        }

        return LoadDefaultBitmapFromPath(defaultPath);
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
        try
        {
            if (path.StartsWith("avares://", StringComparison.Ordinal))
            {
                var uri = new Uri(path);
                using var stream = AssetLoader.Open(uri);
                return new Bitmap(stream);
            }

            var fullPath = ResolveFullPath(path);
            using var fs = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return new Bitmap(fs);
        }
        catch (Exception ex)
        {
            Logger.Error($"默认图片加载失败：{path}", ex);
            throw;
        }
    }

    private static string ResolveFullPath(string filePath)
    {
        var fullPath = Path.IsPathRooted(filePath)
            ? filePath
            : Path.Combine(FileHelper.GetRootDirectory(), filePath);

        if (!File.Exists(fullPath))
            throw new FileNotFoundException("图片文件不存在", fullPath);

        return fullPath;
    }

    private static Bitmap LoadCore(string filePath)
    {
        if (filePath.StartsWith("avares://", StringComparison.Ordinal))
        {
            var uri = new Uri(filePath);
            using var stream = AssetLoader.Open(uri);
            return new Bitmap(stream);
        }

        var fullPath = ResolveFullPath(filePath);
        using var fs = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return new Bitmap(fs);
    }

    private static byte[] ReadFileBytes(string filePath)
    {
        if (filePath.StartsWith("avares://", StringComparison.Ordinal))
        {
            var uri = new Uri(filePath);
            using var stream = AssetLoader.Open(uri);
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            return ms.ToArray();
        }

        var fullPath = ResolveFullPath(filePath);
        return File.ReadAllBytes(fullPath);
    }

    private static Bitmap BytesToBitmap(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes);
        return new Bitmap(ms);
    }

    internal class BitmapLruCache
    {
        private class Entry
        {
            public string Key { get; set; } = null!;
            public Bitmap Bitmap { get; set; } = null!;
            public LinkedListNode<string> Node { get; set; } = null!;
            public long Memory { get; set; }
            public int RefCount;
            public volatile bool IsCached;
        }

        private readonly Dictionary<string, Entry> _cache = new();
        private readonly LinkedList<string> _lru = new();
        private readonly ConcurrentDictionary<string, DateTime> _failed = new();
        private readonly ConcurrentDictionary<Bitmap, Entry> _bitmapToEntry = new();
        private readonly object _lock = new();
        private long _estimatedMemory;
        private const int MaxCount = 50;
        private const long MaxMemory = 32L * 1024 * 1024;
        private static readonly TimeSpan FailedRetryCooldown = TimeSpan.FromSeconds(30);

        public bool TryGet(string key, out Bitmap? bitmap)
        {
            if (_failed.TryGetValue(key, out var failedTime))
            {
                if (DateTime.UtcNow - failedTime < FailedRetryCooldown)
                {
                    bitmap = null;
                    return false;
                }
                _failed.TryRemove(key, out _);
            }

            Entry? entry;

            lock (_lock)
            {
                if (!_cache.TryGetValue(key, out entry))
                {
                    bitmap = null;
                    return false;
                }
                _lru.Remove(entry.Node);
                _lru.AddFirst(entry.Node);
            }

            Interlocked.Increment(ref entry.RefCount);
            bitmap = entry.Bitmap;
            return true;
        }

        public bool IsFailed(string key)
        {
            if (!_failed.TryGetValue(key, out var timestamp))
                return false;

            if (DateTime.UtcNow - timestamp < FailedRetryCooldown)
                return true;

            _failed.TryRemove(key, out _);
            return false;
        }

        /// <summary>
        /// 将图片加入缓存并返回最终应使用的 Bitmap 实例。
        /// 若并发加载了同一图片，返回已缓存实例并增加其引用计数。
        /// </summary>
        public Bitmap Add(string key, Bitmap bitmap)
        {
            var memory = (long)bitmap.PixelSize.Width * bitmap.PixelSize.Height * 4L;
            _failed.TryRemove(key, out _);

            lock (_lock)
            {
                // 并复用已有条目，丢弃新 bitmap
                if (_cache.TryGetValue(key, out var existing))
                {
                    Interlocked.Increment(ref existing.RefCount);
                    bitmap.Dispose();
                    return existing.Bitmap;
                }

                // 淘汰旧条目（按 MaxCount / MaxMemory）
                while (_cache.Count >= MaxCount || Interlocked.Read(ref _estimatedMemory) + memory > MaxMemory)
                {
                    var last = _lru.Last;
                    if (last == null) break;

                    if (_cache.Remove(last.Value, out var evict))
                    {
                        _lru.RemoveLast();
                        Interlocked.Add(ref _estimatedMemory, -evict.Memory);
                        evict.IsCached = false;
                        // 引用仍由外部持有，此处不 Dispose
                    }
                }

                var node = _lru.AddFirst(key);
                var entry = new Entry
                {
                    Key = key,
                    Bitmap = bitmap,
                    Node = node,
                    Memory = memory,
                    RefCount = 1,
                    IsCached = true
                };
                _cache[key] = entry;
                _bitmapToEntry[bitmap] = entry;
                Interlocked.Add(ref _estimatedMemory, memory);
            }

            return bitmap;
        }

        public void Release(Bitmap? bitmap)
        {
            if (bitmap == null) return;

            if (!_bitmapToEntry.TryGetValue(bitmap, out var entry))
                return;

            var newRef = Interlocked.Decrement(ref entry.RefCount);
            if (newRef > 0) return;

            // 引用归零，需要清理
            if (!entry.IsCached)
            {
                _bitmapToEntry.TryRemove(bitmap, out _);
                entry.Bitmap.Dispose();
                return;
            }

            // 锁内移除缓存结构
            lock (_lock)
            {
                if (!entry.IsCached) return;

                _lru.Remove(entry.Node);
                _cache.Remove(entry.Key);
                Interlocked.Add(ref _estimatedMemory, -entry.Memory);
                entry.IsCached = false;
            }

            // 若锁期间有其他线程 TryGet 获取了引用，不 Dispose
            if (Interlocked.CompareExchange(ref entry.RefCount, 0, 0) <= 0)
            {
                _bitmapToEntry.TryRemove(bitmap, out _);
                entry.Bitmap.Dispose();
            }
        }

        public void MarkFailed(string key)
        {
            // 无需检查 _cache：已成功缓存的图片不会走到这里
            _failed[key] = DateTime.UtcNow;

            if (_failed.Count > 200)
            {
                var cutoff = DateTime.UtcNow - FailedRetryCooldown;
                var oldItems = _failed.Where(x => x.Value < cutoff)
                    .Take(100)
                    .Select(x => x.Key)
                    .ToList();
                foreach (var k in oldItems) _failed.TryRemove(k, out _);
            }
        }

        public void Clear()
        {
            List<Entry> entries;
            lock (_lock)
            {
                entries = _cache.Values.ToList();
                _cache.Clear();
                _lru.Clear();
                Interlocked.Exchange(ref _estimatedMemory, 0);
            }

            _failed.Clear();
            _bitmapToEntry.Clear();

            foreach (var entry in entries)
            {
                entry.IsCached = false;
                if (Interlocked.CompareExchange(ref entry.RefCount, 0, 0) <= 0)
                {
                    entry.Bitmap.Dispose();
                }
            }
        }
    }
}

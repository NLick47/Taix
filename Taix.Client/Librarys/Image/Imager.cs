using System;
using System.IO;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Taix.Client.Logging;
using Taix.Client.Shared.Librarys;

namespace Taix.Client.Librarys.Image;

public class Imager
{
    private static Bitmap? _defaultBitmap;
    private static readonly object DefaultLock = new();
    private const string DefaultAssetPath = "avares://Taix/Resources/Icons/defaultIcon.png";

    public static Bitmap Load(string filePath, string? defaultPath = null)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            Logger.Warn("图片路径为空，返回默认图片");
            return GetDefaultBitmap(defaultPath);
        }

        Bitmap? bitmap = null;
        try
        {
            bitmap = LoadCore(filePath);
        }
        catch (Exception ex)
        {
            Logger.Error($"无法加载图片：{filePath}，已回退到默认图片", ex);
        }

        return bitmap ?? GetDefaultBitmap(defaultPath);
    }

    private static Bitmap LoadCore(string filePath)
    {
        if (filePath.StartsWith("avares://", StringComparison.Ordinal))
        {
            var uri = new Uri(filePath);
            using var stream = AssetLoader.Open(uri);
            return new Bitmap(stream);
        }

        var fullPath = Path.IsPathRooted(filePath)
            ? filePath
            : Path.Combine(FileHelper.GetRootDirectory(), filePath);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("图片文件不存在", fullPath);
        }

        using var fs = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return new Bitmap(fs);
    }

    private static Bitmap GetDefaultBitmap(string? customDefaultPath)
    {
        var path = customDefaultPath ?? DefaultAssetPath;

        if (customDefaultPath is null && _defaultBitmap is not null)
            return _defaultBitmap;

        lock (DefaultLock)
        {
            if (customDefaultPath is null && _defaultBitmap is not null)
                return _defaultBitmap;

            try
            {
                var uri = new Uri(path);
                using var stream = AssetLoader.Open(uri);
                var bitmap = new Bitmap(stream);

                if (customDefaultPath is null)
                    _defaultBitmap = bitmap;

                return bitmap;
            }
            catch (Exception ex)
            {
                Logger.Error($"默认图片也无法加载：{path}", ex);
                throw;
            }
        }
    }
}
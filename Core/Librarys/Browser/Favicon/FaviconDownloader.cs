using System.Net;
using SharedLibrary.Librarys;
using SkiaSharp;

namespace Core.Librarys.Browser.Favicon;

public class FaviconDownloader
{
    /// <summary>
    ///     下载文件
    /// </summary>
    /// <param name="url_">文件远程地址</param>
    /// <param name="savePath_">本地保存路径</param>
    public static async Task<string> DownloadAsync(string url_, string saveName_)
    {
        if (string.IsNullOrEmpty(url_)) return string.Empty;

        var savePath = Path.Combine(FileHelper.GetRootDirectory(), "WebFavicons", saveName_ + Path.GetExtension(url_));
        var dir = Path.GetDirectoryName(savePath);


        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        if (File.Exists(savePath)) return savePath;
        try
        {
            using (var web = new WebClient())
            {
                if (Path.GetExtension(url_) == ".svg")
                {
                    var pngBytes = ConvertSvgToPng(await web.DownloadDataTaskAsync(url_), 32, 32);
                    await File.WriteAllBytesAsync(savePath, pngBytes);
                }
                else
                {
                    await web.DownloadFileTaskAsync(url_, savePath);
                }

                return savePath;
            }
        }
        catch (Exception e)
        {
            Logger.Error("下载图标失败，" + e);
            return string.Empty;
        }
    }


    public static byte[] ConvertSvgToPng(byte[] svgBytes, int? width = null, int? height = null,
        SKColor? background = null)
    {
        using var svgStream = new MemoryStream(svgBytes);
        var svg = new SKSvg();

        svg.Load(svgStream);

        var scaledWidth = width ?? (int)svg.Picture.CullRect.Width;
        var scaledHeight = height ?? (int)svg.Picture.CullRect.Height;

        var imageInfo = new SKImageInfo(scaledWidth, scaledHeight);
        using var surface = SKSurface.Create(imageInfo);
        var canvas = surface.Canvas;

        if (background.HasValue) canvas.Clear(background.Value);

        var matrix = SKMatrix.CreateScale(
            scaledWidth / svg.Picture.CullRect.Width,
            scaledHeight / svg.Picture.CullRect.Height
        );
        canvas.DrawPicture(svg.Picture, ref matrix);

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var memoryStream = new MemoryStream();
        data.SaveTo(memoryStream);
        return memoryStream.ToArray();
    }
}
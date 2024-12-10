using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Core.Librarys;
using Infrastructure.Librarys;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UI.Librays.Image
{
    public class Imager
    {
        public static Bitmap Load(string filePath, string defaultPath = "avares://Taix/Resources/Icons/defaultIcon.png")
        {
            try
            {
                if (string.IsNullOrEmpty(filePath))
                {
                    throw new ArgumentNullException(nameof(filePath));
                }

                Bitmap bitmap;
                if (filePath.IndexOf("avares://") == 0)
                {
                    var uri = new Uri(filePath);
                    using (var stream = AssetLoader.Open(uri))
                    {
                        bitmap = new Bitmap(stream);
                    }
                }
                else
                {
                    // 检查是否为绝对路径，如果不是，则组合为绝对路径
                    if (Path.IsPathRooted(filePath) == false)
                    {
                        filePath = Path.Combine(FileHelper.GetRootDirectory(), filePath);
                    }

                    if (!File.Exists(filePath))
                    {
                        throw new FileNotFoundException(filePath);
                    }

                    using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                    {
                        bitmap = new Bitmap(fileStream);
                    }
                }

                return bitmap;
            }
            catch (Exception ec)
            {
                // 如果出错，返回默认图标
                Bitmap defaultBitmap;
                var defaultUri = new Uri(defaultPath);
                using (var stream = AssetLoader.Open(defaultUri))
                {
                    defaultBitmap = new Bitmap(stream);
                }

                Logger.Error("无法读取图片：" + filePath + "。" + ec.Message);

                return defaultBitmap;
            }
        }
    }
}

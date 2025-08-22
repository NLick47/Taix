using System.Diagnostics;
using System.Text.RegularExpressions;
using SharedLibrary.Librarys;
using Toolbelt.Drawing;

namespace Core.Librarys;

public class Iconer
{
    /// <summary>
    ///     格式化图标文件名称
    /// </summary>
    /// <param name="processname">进程名称</param>
    /// <param name="desc">进程简介</param>
    /// <returns>返回正确的文件名称</returns>
    private static string FromatIconFileName(string processname, string desc)
    {
        var iconName = (processname + desc).Replace(" ", "") + ".png";

        //  清除无效字符
        iconName = iconName.Replace("/", "");
        iconName = iconName.Replace("\\", "");
        iconName = iconName.Replace(":", "");
        iconName = iconName.Replace("*", "");
        iconName = iconName.Replace("?", "");
        iconName = iconName.Replace("\"", "");
        iconName = iconName.Replace("'", "");
        iconName = iconName.Replace("<", "");
        iconName = iconName.Replace(">", "");
        iconName = iconName.Replace("|", "");

        return iconName;
    }

    public static string Get(string processname, string desc, bool isRelativePath = true)
    {
        var iconName = FromatIconFileName(processname, desc);
        var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
            "AppIcons", iconName);
        if (!File.Exists(iconPath)) return "avares://Taix/Resources/Icons/defaultIcon.png";

        if (isRelativePath)
            return Path.Combine(
                "AppIcons", iconName);
        return iconPath;
    }

    /// <summary>
    ///     提取icon为Png格式并保存到程序目录下
    /// </summary>
    /// <param name="file"></param>
    /// <param name="processname"></param>
    /// <param name="desc"></param>
    /// <param name="isRelativePath">是否返回相对路径</param>
    /// <returns>返回提取到程序目录下的路径</returns>
    public static string ExtractFromFile(string file, string processname, string desc, bool isCheck = true,
        bool isRelativePath = true)
    {
        try
        {
            var iconName = FromatIconFileName(processname, desc);

            var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "AppIcons", iconName);

            var relativePath = Path.Combine(
                "AppIcons", iconName);

            if (isCheck && File.Exists(iconPath))
            {
                if (isRelativePath) return relativePath;
                return iconPath;
            }

            var dir = Path.GetDirectoryName(iconPath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            //  uwp app icon handle

            if (file.IndexOf("WindowsApps") != -1)
            {
                //  只有在包含此关键字时才去处理

                //  继续判断是否是uwp程序
                var appdir = file.Substring(0, file.Length - file.Split('\\').Last().Length);
                var appxManifestPath = appdir + "AppxManifest.xml";
                if (File.Exists(appxManifestPath))
                {
                    //  是uwp程序
                    Debug.WriteLine("is uwp!" + appxManifestPath);
                    //  读取描述文件
                    var manifestText = File.ReadAllText(appxManifestPath);
                    var match = Regex.Match(manifestText, @"<Logo>(.*?)</Logo>");
                    var logoName = match.Groups[1].Value;

                    var iconFile = string.Empty;

                    var logo100 = logoName.Replace(".png", ".scale-100.png");
                    var logo125 = logoName.Replace(".png", ".scale-125.png");
                    var logo150 = logoName.Replace(".png", ".scale-150.png");
                    var logo200 = logoName.Replace(".png", ".scale-200.png");
                    var logo400 = logoName.Replace(".png", ".scale-400.png");

                    if (File.Exists(appdir + logo100))
                        iconFile = appdir + logo100;
                    else if (File.Exists(appdir + logo125))
                        iconFile = appdir + logo125;
                    else if (File.Exists(appdir + logo150))
                        iconFile = appdir + logo150;
                    else if (File.Exists(appdir + logo200))
                        iconFile = appdir + logo200;
                    else if (File.Exists(appdir + logo400))
                        iconFile = appdir + logo400;
                    else
                        return string.Empty;

                    if (!string.IsNullOrEmpty(iconFile) && File.Exists(iconFile))
                    {
                        //  copy to tai dir

                        File.Copy(iconFile, iconPath);

                        if (isRelativePath) return relativePath;

                        return iconPath;
                    }

                    return string.Empty;
                }
            }

            //  exe app icon handle
            try
            {
                using var s = File.Create(iconPath);
                IconExtractor.Extract1stIconTo(file, s);
                return isRelativePath ? relativePath : iconPath;
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message + "，File: " + file + "，Process: " + processname);
                return string.Empty;
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex.Message + "，File: " + file + "，Process: " + processname);
            return string.Empty;
        }
    }
}
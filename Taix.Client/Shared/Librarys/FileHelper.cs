using System;
using System.IO;

namespace Taix.Client.Shared.Librarys;

/// <summary>
/// 文件操作帮助类
/// </summary>
public class FileHelper
{
    private static string? _rootDirectory;

    /// <summary>
    /// 获取根目录
    /// </summary>
    /// <returns></returns>
    public static string GetRootDirectory()
    {
        if (_rootDirectory != null)
            return _rootDirectory;

#if MACOS
        _rootDirectory = "/Applications/TaixTools";
#else
        _rootDirectory = AppContext.BaseDirectory;
#endif
        return _rootDirectory;
    }
}
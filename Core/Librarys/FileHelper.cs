namespace Core.Librarys;

/// <summary>
///     文件操作帮助类
/// </summary>
public class FileHelper
{
    /// <summary>
    ///     获取根目录
    /// </summary>
    /// <returns></returns>
    public static string GetRootDirectory()
    {
        return AppContext.BaseDirectory;
    }
}
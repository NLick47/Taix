using SharedLibrary.Models.AppObserver;

namespace SharedLibrary.Servicers;

public interface IAppManager
{
    /// <summary>
    ///     通过窗口句柄获取应用信息
    /// </summary>
    /// <param name="hwnd_">窗口句柄</param>
    /// <returns></returns>
    AppInfo GetAppInfo(IntPtr hwnd_);
}
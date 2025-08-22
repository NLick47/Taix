using System.Text;
using SharedLibrary.Models.AppObserver;
using SharedLibrary.Servicers;

namespace Win;

public class WindowManager : IWindowManager
{
    public WindowInfo GetWindowInfo(nint handle_)
    {
        try
        {
            var title = GetWindowTitle(handle_);
            var rect = Win32API.GetWindowRect(handle_);
            var className = Win32API.GetWindowClassName(handle_);
            var width = rect.Width;
            var height = rect.Height;
            var x = rect.Left;
            var y = rect.Top;

            return new WindowInfo(className, title, handle_, width, height, x, y);
        }
        catch (Exception e)
        {
            return WindowInfo.Empty;
        }
    }

    /// <summary>
    ///     获取窗口标题
    /// </summary>
    /// <param name="handle_"></param>
    /// <returns></returns>
    private string GetWindowTitle(nint handle_)
    {
        try
        {
            var titleCapacity = Win32API.GetWindowTextLength(handle_) * 2;
            var stringBuilder = new StringBuilder(titleCapacity);
            Win32API.GetWindowText(handle_, stringBuilder, stringBuilder.Capacity);
            var title = stringBuilder.ToString();
            return stringBuilder.ToString();
        }
        catch (Exception e)
        {
            return string.Empty;
        }
    }
}
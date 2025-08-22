using SharedLibrary.Models.AppObserver;
using SharedLibrary.Servicers;

namespace Linux;

public class XManager : IWindowManager
{
    public WindowInfo GetWindowInfo(nint handle_)
    {
        return WindowInfo.Empty;
    }
}
using SharedLibrary.Models.AppObserver;
using SharedLibrary.Servicers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Linux
{
    public class XAppManager : IAppManager
    {
        public AppInfo GetAppInfo(nint hwnd_)
        {
            return AppInfo.Empty;
        }
    }
}

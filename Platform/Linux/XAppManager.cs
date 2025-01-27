using SharedLibrary.Models.AppObserver;
using SharedLibrary.Servicers;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Linux
{
    public class XAppManager : IAppManager
    {
        private readonly IntPtr _display;
        public XAppManager()
        {
            _display = Xlib.XOpenDisplay(IntPtr.Zero);
        }
        
        public AppInfo GetAppInfo(nint hwnd_)
        {
            GetWindowProcessId(_display,hwnd_, out var processId);
            return AppInfo.Empty;
        }
        private Dictionary<string, AppInfo> _apps;
        private readonly ConcurrentDictionary<int, (string Name, DateTime LastChecked)> _processNameCache;
        private int _outTime = 5000;
         private bool GetWindowProcessId(IntPtr display, IntPtr handle, out int processId)
        {
            processId = 0;
            IntPtr pidAtom = Xlib.XInternAtom(display, "_NET_WM_PID", true);
            if (pidAtom == IntPtr.Zero)
            {
                return false;
            }
            IntPtr card32Atom = Xlib.XInternAtom(display, "CARD32", true);
            // if (card32Atom == IntPtr.Zero)
            // {
            //     Console.WriteLine("无法获取 CARD32 原子");
            //     return false;
            // }

            IntPtr actualType;
            int actualFormat;
            IntPtr nitems, bytesAfter;
            IntPtr propReturn;

            int result = Xlib.XGetWindowProperty(display, handle, pidAtom, IntPtr.Zero, new IntPtr(4), false,(IntPtr) Atom.AnyPropertyType, out actualType, out actualFormat, out nitems, out bytesAfter, out propReturn);
            if (result != 0)
            {
                return false;
            }

            if (actualType == Xlib.XInternAtom(display, "CARD32", true) && actualFormat == 32 && nitems == 1)
            {
                processId = Marshal.ReadInt32(propReturn);
                Xlib.XFree(propReturn);
                return true;
            }

            Xlib.XFree(propReturn);
            return false;
        }

        private string? GetAppProcessName(int processId_)
        {
            if (!_processNameCache.TryGetValue(processId_, out var val) ||
                (DateTime.Now - val.LastChecked).TotalSeconds > 600)
            {
                using var process = Process.GetProcessById(processId_);
                if (!string.IsNullOrEmpty(process?.ProcessName))
                {
                    _processNameCache[processId_] = (process.ProcessName, DateTime.Now);
                }
                return process?.ProcessName;
            }
            return _processNameCache[processId_].Name;
        }

        private string GetAppExecutablePath(string processName_)
        {
            var process = Process.GetProcessesByName(processName_).FirstOrDefault();
            if (process != null)
            {
                return process.MainModule?.FileName;
            }
            return string.Empty;
        }

        private bool IsSystemComponent(string processName_)
        {
            bool isSys = sysProcessSet.Contains(processName_);
            return isSys;
        }
        
        private static readonly HashSet<string> sysProcessSet = new()
        {
            "ShellExperienceHost",
            "StartMenuExperienceHost",
            "SearchHost",
            "LockApp"
        };
    }
}

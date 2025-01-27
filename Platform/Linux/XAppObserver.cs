using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using SharedLibrary.Event;
using SharedLibrary.Servicers;

namespace Linux
{
    public class XAppObserver : IAppObserver
    {
        public event AppObserverEventHandler OnAppActiveChanged;
        
        private IntPtr _display;
        private bool _isStart;
        private readonly IAppManager _appManager;
        private readonly IWindowManager _windowManager;

        public XAppObserver(IAppManager appManager_, IWindowManager windowManager)
        {
            _appManager = appManager_;
            _windowManager = windowManager;
            _display = Xlib.XOpenDisplay(IntPtr.Zero);
            if (_display == IntPtr.Zero)
            {
                throw new Exception("Failed to open display");
            }
        }

        public void Start()
        {
            if (_isStart)
            {
                return;
            }
            _isStart = true;
            var defaultScreen = Xlib.XDefaultScreen(_display);
            var rootWindow =  Xlib.XRootWindow(_display,defaultScreen);
            Xlib.XSelectInput(_display, rootWindow, EventMask.FocusChangeMask);
            _ = Task.Run(() => MonitorFocusChanges());
        }

        public void Stop()
        {
            if (!_isStart) return;
            _isStart = false;
            Xlib.XCloseDisplay(_display);
        }

      
        private void MonitorFocusChanges()
        {
            var ev = Marshal.AllocHGlobal(24 * sizeof(long));
                while (_isStart)
                {
                    if (Xlib.Pending(_display) > 0)
                    {
                        Xlib.XNextEvent(_display,  ev);
                        var xevent = Marshal.PtrToStructure<XAnyEvent>(ev);
                        // if (xevent.type == (int)Event.FocusIn)
                        // {
                        //     var args = GetAppInfoEventArgs(xevent.window, DateTime.Now);
                        //     OnAppActiveChanged?.Invoke(this, args);
                        // }
                        Debug.Write(ev);
                    }
                }
        }
        private AppActiveChangedEventArgs GetAppInfoEventArgs(IntPtr handle_, DateTime activeTime_)
        {
            var app = _appManager.GetAppInfo(handle_);
            var window = _windowManager.GetWindowInfo(handle_);
            return new AppActiveChangedEventArgs(app, window, activeTime_);
        }
    }
}

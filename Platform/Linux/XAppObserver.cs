using System.Runtime.InteropServices;
using SharedLibrary.Event;
using SharedLibrary.Servicers;

namespace Linux;

public class XAppObserver : IAppObserver
{
    private readonly IAppManager _appManager;

    private readonly IntPtr _display;
    private readonly IWindowManager _windowManager;
    private IntPtr _defaultRootWindow;
    private bool _isStart;

    public XAppObserver(IAppManager appManager_, IWindowManager windowManager)
    {
        _appManager = appManager_;
        _windowManager = windowManager;
        _display = Xlib.XOpenDisplay(IntPtr.Zero);
        if (_display == IntPtr.Zero) throw new Exception("Failed to open display");
    }

    public event AppObserverEventHandler OnAppActiveChanged;

    public void Start()
    {
        if (_isStart) return;
        _isStart = true;
        var defaultScreen = Xlib.XDefaultScreen(_display);
        _defaultRootWindow = Xlib.XRootWindow(_display, defaultScreen);
        Xlib.XSelectInput(_display, _defaultRootWindow, EventMask.SubstructureNotifyMask);
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
            if (Xlib.Pending(_display) > 0)
            {
                Xlib.XNextEvent(_display, ev);
                var xevent = Marshal.PtrToStructure<XAnyEvent>(ev);
                if (xevent.type == (int)Event.CreateNotify ||
                    xevent.type == (int)Event.DestroyNotify)
                {
                    var activeWindowHandle = GetActiveWindowHandle();
                    var args = GetAppInfoEventArgs(activeWindowHandle, DateTime.Now);
                    OnAppActiveChanged?.Invoke(this, args);
                }
            }
    }

    private IntPtr GetActiveWindowHandle()
    {
        var activeWindowAtom = Xlib.XInternAtom(_display, "_NET_ACTIVE_WINDOW", false);
        if (Xlib.XGetWindowProperty(_display, _defaultRootWindow, activeWindowAtom, 0, 1, false,
                (IntPtr)Atom.AnyPropertyType, out _, out _, out _, out _, out var prop) == 0)
            return Marshal.ReadIntPtr(prop);

        return IntPtr.Zero;
    }


    private AppActiveChangedEventArgs GetAppInfoEventArgs(IntPtr handle_, DateTime activeTime_)
    {
        var app = _appManager.GetAppInfo(handle_);
        var window = _windowManager.GetWindowInfo(handle_);
        return new AppActiveChangedEventArgs(app, window, activeTime_);
    }
}
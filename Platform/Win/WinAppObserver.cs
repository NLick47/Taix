using System.Diagnostics;
using System.Runtime.InteropServices;
using SharedLibrary.Enums;
using SharedLibrary.Event;
using SharedLibrary.Servicers;

namespace Win;

public class WinAppObserver : IAppObserver
{
    private const int delayDuration = 1000;
    private readonly IAppManager _appManager;

    //  获得焦点事件
    private readonly WinEventDelegate _foregroundEventDelegate;
    private readonly IWindowManager _windowManager;
    private nint _hook;

    private bool _isProcessing;
    private bool _isStart;

    public WinAppObserver(IAppManager appManager_, IWindowManager windowManager)
    {
        _appManager = appManager_;
        _windowManager = windowManager;
        _foregroundEventDelegate = ForegroundEventCallback;
    }

    public event AppObserverEventHandler OnAppActiveChanged;

    public void Start()
    {
        if (_isStart) return;
        _isStart = true;
        _hook = SetWinEventHook(0x0003, 0x0003, nint.Zero, _foregroundEventDelegate, 0, 0, 0);
        HandleForegroundWindow();
    }

    public void Stop()
    {
        if (!_isStart) return;
        _isStart = false;
        if (_hook != nint.Zero)
        {
            UnhookWinEvent(_hook);
            _hook = nint.Zero;
        }
    }

    private async void ForegroundEventCallback(nint hWinEventHook, uint eventType, nint hwnd, int idObject, int idChild,
        uint dwEventThread, uint dwmsEventTime)
    {
        if (_isProcessing) return;
        _isProcessing = true;
        var activeTime = DateTime.Now;
        var args = GetAppInfoEventArgs(hwnd, activeTime);
        Debug.WriteLine(activeTime);
        Debug.WriteLine(args.App.ToString());
        //  响应事件
        OnAppActiveChanged?.Invoke(this, args);
        if (args.App.Type == AppType.SystemComponent)
        {
            await Task.Delay(delayDuration);
            DelayDetect();
        }

        _isProcessing = false;
    }

    private void DelayDetect()
    {
        var activeTime = DateTime.Now;
        var w = Win32API.GetForegroundWindow();
        var args = GetAppInfoEventArgs(w, activeTime);
        if (args.App.Type != AppType.SystemComponent)
            //  响应事件
            OnAppActiveChanged?.Invoke(this, args);
    }

    private AppActiveChangedEventArgs GetAppInfoEventArgs(nint handle_, DateTime activeTime_)
    {
        var app = _appManager.GetAppInfo(handle_);
        var window = _windowManager.GetWindowInfo(handle_);
        return new AppActiveChangedEventArgs(app, window, activeTime_);
    }

    private void HandleForegroundWindow()
    {
        var w = Win32API.GetForegroundWindow();
        ForegroundEventCallback(nint.Zero, 0, w, 0, 0, 0, 0);
    }

    #region win32 api

    public delegate void WinEventDelegate(nint hWinEventHook, uint eventType,
        nint hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

    [DllImport("user32.dll")]
    public static extern nint SetWinEventHook(uint eventMin, uint eventMax, nint
            hmodWinEventProc, WinEventDelegate lpfnWinEventProc, uint idProcess,
        uint idThread, uint dwFlags);

    [DllImport("user32.dll")]
    public static extern bool UnhookWinEvent(nint hWinEventHook);

    #endregion
}
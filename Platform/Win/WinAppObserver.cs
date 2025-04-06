using SharedLibrary.Enums;
using SharedLibrary.Event;
using SharedLibrary.Servicers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Win
{
    public class WinAppObserver : IAppObserver
    {
        public event AppObserverEventHandler OnAppActiveChanged;

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

        //  获得焦点事件
        private readonly WinEventDelegate _foregroundEventDelegate;
        private readonly IAppManager _appManager;
        private readonly IWindowManager _windowManager;
        private nint _hook;
        private bool _isStart = false;
      
        public WinAppObserver(IAppManager appManager_, IWindowManager windowManager)
        {
            _appManager = appManager_;
            _windowManager = windowManager;
            _foregroundEventDelegate = new WinEventDelegate(ForegroundEventCallback);
        }

        const int delayDuration = 1000;

        private bool _isProcessing = false;

        private async void ForegroundEventCallback(nint hWinEventHook, uint eventType, nint hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            if (_isProcessing) return;
            _isProcessing = true;
            DateTime activeTime = DateTime.Now;
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
            DateTime activeTime = DateTime.Now;
            nint w = Win32API.GetForegroundWindow();
            var args = GetAppInfoEventArgs(w, activeTime);
            if (args.App.Type != AppType.SystemComponent)
            {
                //  响应事件
                OnAppActiveChanged?.Invoke(this, args);
            }
        }

        private AppActiveChangedEventArgs GetAppInfoEventArgs(nint handle_, DateTime activeTime_)
        {
            var app = _appManager.GetAppInfo(handle_);
            var window = _windowManager.GetWindowInfo(handle_);
            return new AppActiveChangedEventArgs(app, window, activeTime_);
        }

        private void HandleForegroundWindow()
        {
            nint w = Win32API.GetForegroundWindow();
            ForegroundEventCallback(nint.Zero, 0, w, 0, 0, 0, 0);
        }

        public void Start()
        {
            if (_isStart)
            {
                return;
            }
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
    }
}

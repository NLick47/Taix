using System.Diagnostics;
using System.Drawing;
using Avalonia.Threading;
using Microsoft.Win32;
using SharedLibrary.Enums;
using SharedLibrary.Event;
using SharedLibrary.Librarys;
using SharedLibrary.Servicers;

namespace Win;

public class WinSleepdiscover : ISleepdiscover
{
    private static readonly nint _hookKeyboardId = nint.Zero;
    private static readonly nint _hookMouseId = nint.Zero;

    //  键盘钩子
    private readonly Win32API.LowLevelKeyboardProc _keyboardProc;


    //  鼠标钩子
    private readonly Win32API.LowLevelKeyboardProc _mouseProc;

    private int _emptyPointNum;

    private Point _lastPoint;
    private nint _mouseHook;

    /// <summary>
    ///     播放声音开始时间
    /// </summary>
    private DateTime _playSoundStartTime;

    /// <summary>
    ///     最后一次按键时间
    /// </summary>
    private DateTime _pressKeyboardLastTime;

    private SleepStatus _status = SleepStatus.Wake;

    private DispatcherTimer _timer;

    public WinSleepdiscover()
    {
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        SystemEvents.SessionSwitch += SystemEvents_SessionSwitch;

        _keyboardProc = HookCallback;
        _mouseProc = HookMouseCallback;
    }

    public event SleepdiscoverEventHandler SleepStatusChanged;

    public void Start()
    {
        StartTimer();

        _lastPoint = Win32API.GetCursorPosition();
        _playSoundStartTime = DateTime.MinValue;
        _pressKeyboardLastTime = DateTime.Now;

        //  设置键盘钩子
        Win32API.SetKeyboardHook(_keyboardProc);
    }

    public void Stop()
    {
        StopTimer();
    }

    private void StartTimer()
    {
        StopTimer();
        _timer = new DispatcherTimer();
        _timer.Interval = new TimeSpan(0, 5, 0);
        _timer.Tick += Timer_Tick;
        _timer.Start();
    }

    private void StopTimer()
    {
        if (_timer != null)
        {
            _timer.Tick -= Timer_Tick;
            _timer.Stop();
        }
    }

    private void SystemEvents_SessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        if (e.Reason == SessionSwitchReason.RemoteDisconnect || e.Reason == SessionSwitchReason.ConsoleDisconnect)
            //  与这台设备远程桌面连接断开
            if (_status == SleepStatus.Wake)
            {
                Logger.Warn("与这台设备远程桌面连接断开");
                Sleep();
            }
    }

    private nint HookMouseCallback(int nCode, nint wParam, nint lParam)
    {
        if (nCode >= 0 && _status == SleepStatus.Sleep)
            if (wParam == Win32API.WM_LBUTTONDBLCLK || wParam == Win32API.WM_WHEEL)
            {
                Logger.Info("鼠标唤醒");

                Wake();
            }

        return Win32API.CallNextHookEx(_hookMouseId, nCode, wParam, lParam);
    }

    private nint HookCallback(
        int nCode, nint wParam, nint lParam)
    {
        if (nCode >= 0 && wParam == Win32API.WM_KEYDOWN)
        {
            if (_status == SleepStatus.Sleep)
            {
                Logger.Info("键盘唤醒");

                Wake();
            }
            else
            {
                _playSoundStartTime = DateTime.MinValue;
                _pressKeyboardLastTime = DateTime.Now;
            }
        }

        return Win32API.CallNextHookEx(_hookKeyboardId, nCode, wParam, lParam);
    }

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        switch (e.Mode)
        {
            case PowerModes.Suspend:
                //  电脑休眠
                if (_status == SleepStatus.Wake)
                {
                    Logger.Info("设备已休眠");

                    Sleep();
                }

                break;
            case PowerModes.Resume:
                //  电脑恢复
                if (_status == SleepStatus.Sleep)
                {
                    Logger.Info("设备已恢复");

                    Wake();
                }

                break;
        }
    }


    #region 指示当前是否处于睡眠状态

    /// <summary>
    ///     指示当前是否处于睡眠状态
    /// </summary>
    /// <returns>睡眠返回true</returns>
    private async Task<bool> IsSleepAsync()
    {
        var point = Win32API.GetCursorPosition();

        if (point.X + point.Y == 0)
        {
            _emptyPointNum++;

            if (_emptyPointNum == 2)
            {
                _emptyPointNum = 0;
                return true;
            }
        }

        var isActive = await IsActiveAsync();
        if (isActive) return false;

        //  鼠标和键盘都没有操作时判断是否在播放声音

        //  持续30秒检测当前是否在播放声音
        var isPlaySound = await IsPlaySoundAsync();

        if (!isPlaySound)
            //  没有声音判定为睡眠状态
            return true;

        //  在播放声音
        if (_playSoundStartTime == DateTime.MinValue)
        {
            //  第一次记录声音开始时间
            _playSoundStartTime = DateTime.Now;
            return false;
        }

        //  声音播放超过两个小时视为睡眠状态
        var timeSpan = DateTime.Now - _playSoundStartTime;

        if (timeSpan.TotalHours >= 2)
        {
            //  重置声音开始时间
            _playSoundStartTime = DateTime.MinValue;
            return true;
        }

        return false;
    }

    #endregion

    private async void Timer_Tick(object sender, EventArgs e)
    {
        Debug.WriteLine("检测");
        var isSleep = await IsSleepAsync();
        if (isSleep)
            Sleep();
        else
            _timer.Start();
    }

    private void Sleep()
    {
        if (_status == SleepStatus.Sleep) return;

        //  卸载事件
        SystemEvents.SessionSwitch -= SystemEvents_SessionSwitch;

        //  停止离开检测计时器
        StopTimer();

        _status = SleepStatus.Sleep;

        //  设置鼠标钩子
        Win32API.SetMouseHook(_mouseProc);

        //  状态通知
        SleepStatusChanged?.Invoke(_status);
    }

    private void Wake()
    {
        if (_status == SleepStatus.Wake) return;
        try
        {
            //  注册事件
            SystemEvents.SessionSwitch += SystemEvents_SessionSwitch;

            _status = SleepStatus.Wake;

            //  卸载鼠标钩子
            Win32API.UnhookWindowsHookEx(_mouseHook);

            //  启动离开检测
            StartTimer();


            //  重置声音播放时间
            _playSoundStartTime = DateTime.MinValue;

            //  重置鼠标坐标
            _lastPoint = Win32API.GetCursorPosition();

            //  状态通知
            SleepStatusChanged?.Invoke(_status);
        }
        catch (Exception e)
        {
            Logger.Error(e.ToString());
        }
    }

    /// <summary>
    ///     指示当前是否在播放声音（持续30秒检测）
    /// </summary>
    /// <returns>播放返回true</returns>
    private async Task<bool> IsPlaySoundAsync()
    {
        for (var time = 30; time > 0; time--)
        {
            if (Win32API.IsWindowsPlayingSound()) return true;
            Debug.WriteLine(time);
            await Task.Delay(1000);
        }

        return false;
    }

    /// <summary>
    ///     指示当前是否处于活跃状态（持续30秒检测）
    /// </summary>
    /// <returns>活跃返回true</returns>
    private async Task<bool> IsActiveAsync()
    {
        var result = false;

        await Task.Run(async () =>
        {
            //  持续30秒
            var time = 30;

            while (time > 0)
            {
                var point = Win32API.GetCursorPosition();
                var isMouseMove = _lastPoint.ToString() != point.ToString();
                var isKeyboardActive = !IsKeyboardOuttime();

                if (isMouseMove || isKeyboardActive)
                {
                    _lastPoint = Win32API.GetCursorPosition();
                    result = true;
                    break;
                }

                time--;
                Debug.WriteLine(time);
                await Task.Delay(1000);
            }
        });
        return result;
    }

    /// <summary>
    ///     指示是否键盘行为超时（超过10分钟）
    /// </summary>
    /// <returns>超时返回true</returns>
    private bool IsKeyboardOuttime()
    {
        var timeSpan = DateTime.Now - _pressKeyboardLastTime;
#if DEBUG
        return timeSpan.TotalSeconds >= 10;
#endif
        return timeSpan.TotalMinutes >= 10;
    }
}
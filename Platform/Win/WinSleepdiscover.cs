using Avalonia.Threading;
using Microsoft.Win32;
using SharedLibrary.Enums;
using SharedLibrary.Event;
using SharedLibrary.Librarys;
using SharedLibrary.Servicers;

namespace Win;

public class WinSleepdiscover : ISleepdiscover, IDisposable
{
   
    private long _lastActivityTicks; 
    private int _statusInt;        
    private long _soundStartTimeTicks; 
    
    private nint _keyboardHookId = nint.Zero;
    private nint _mouseHookId = nint.Zero;
    private readonly Win32API.LowLevelKeyboardProc _keyboardProc;
    private readonly Win32API.LowLevelKeyboardProc _mouseProc;
    
#if DEBUG
    private const int ActivityCheckInterval = 30;      // 活动检测间隔
    private const int InactiveThresholdMinutes = 1;    // 无操作判定阈值
    private const int MaxSoundDurationHours = 2;       // 最大声音持续时间
#else
    private const int ActivityCheckInterval = 30;     
    private const int InactiveThresholdMinutes = 5;    
    private const int MaxSoundDurationHours = 2;     
#endif
    
   

    public event SleepdiscoverEventHandler SleepStatusChanged;

    public WinSleepdiscover()
    {
        // 初始化原子状态
        Interlocked.Exchange(ref _lastActivityTicks, DateTime.Now.Ticks);
        Interlocked.Exchange(ref _statusInt, (int)SleepStatus.Wake);
        Interlocked.Exchange(ref _soundStartTimeTicks, 0);
        
        // 初始化钩子回调
        _keyboardProc = HookKeyboardCallback;
        _mouseProc = HookMouseCallback;
        
        // 注册系统事件
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        SystemEvents.SessionSwitch += OnSessionSwitch;
    }

 
    public void Start()
    {
        // 清理可能残留的旧钩子
        CleanupHooks();
        
        // 设置输入设备钩子
        _keyboardHookId = Win32API.SetKeyboardHook(_keyboardProc);
        _mouseHookId = Win32API.SetMouseHook(_mouseProc);
        
        // 重置活动时间
        Interlocked.Exchange(ref _lastActivityTicks, DateTime.Now.Ticks);
        
  
        StartActivityTimer();
        
        // 确保状态正确
        var currentStatus = (SleepStatus)Interlocked.CompareExchange(
            ref _statusInt, 
            (int)SleepStatus.Wake, 
            (int)SleepStatus.Sleep
        );
        
        if (currentStatus != SleepStatus.Wake)
        {
            OnStatusChanged(SleepStatus.Wake);
        }
    }

 
    public void Stop()
    {
        StopActivityTimer();
        CleanupHooks();
        Interlocked.Exchange(ref _statusInt, (int)SleepStatus.Wake);
    }

    
    public void Dispose()
    {
        Stop();
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        SystemEvents.SessionSwitch -= OnSessionSwitch;
    }

    #region 原子状态操作

  
    private void UpdateActivityTime()
    {
        Interlocked.Exchange(ref _lastActivityTicks, DateTime.Now.Ticks);
    }

   
    private DateTime GetLastActivityTime()
    {
        return new DateTime(Interlocked.Read(ref _lastActivityTicks));
    }

   
    private SleepStatus GetStatus()
    {
        return (SleepStatus)Interlocked.CompareExchange(ref _statusInt, 0, 0);
    }

   
    private void SetStatus(SleepStatus newStatus)
    {
        var oldStatus = (SleepStatus)Interlocked.CompareExchange(
            ref _statusInt, 
            (int)newStatus, 
            (int)GetStatus()
        );
        
        if (oldStatus != newStatus)
        {
            OnStatusChanged(newStatus);
        }
    }

 
    private void SetSoundStartTime(DateTime time)
    {
        Interlocked.Exchange(ref _soundStartTimeTicks, time.Ticks);
    }

 
    private DateTime? GetSoundStartTime()
    {
        var ticks = Interlocked.Read(ref _soundStartTimeTicks);
        return ticks > 0 ? new DateTime(ticks) : (DateTime?)null;
    }

   
    private void OnStatusChanged(SleepStatus status)
    {
        Logger.Info(status == SleepStatus.Sleep ? "进入睡眠状态" : "系统唤醒");
        SleepStatusChanged?.Invoke(status);
    }

    #endregion

    #region 钩子回调处理

    private nint HookKeyboardCallback(int nCode, nint wParam, nint lParam)
    {
        if (nCode >= 0 && wParam == Win32API.WM_KEYDOWN)
        {
            UpdateActivityTime();
            
            // 如果当前处于睡眠状态，唤醒系统
            if (GetStatus() == SleepStatus.Sleep)
            {
                Logger.Info("键盘活动 - 唤醒系统");
                SetStatus(SleepStatus.Wake);
            }
        }
        
        return Win32API.CallNextHookEx(_keyboardHookId, nCode, wParam, lParam);
    }

    private nint HookMouseCallback(int nCode, nint wParam, nint lParam)
    {
        if (nCode >= 0)
        {
            bool isMouseActive = wParam == Win32API.WM_MOUSEMOVE ||
                                 wParam == Win32API.WM_LBUTTONDOWN ||
                                 wParam == Win32API.WM_RBUTTONDOWN ||
                                 wParam == Win32API.WM_MBUTTONDOWN ||
                                 wParam == Win32API.WM_MOUSEWHEEL;
            
            if (isMouseActive)
            {
                UpdateActivityTime();
                
                if (GetStatus() == SleepStatus.Sleep)
                {
                    Logger.Info("鼠标活动 - 唤醒系统");
                    SetStatus(SleepStatus.Wake);
                }
            }
        }
        
        return Win32API.CallNextHookEx(_mouseHookId, nCode, wParam, lParam);
    }

    #endregion

    #region 系统事件处理

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        switch (e.Mode)
        {
            case PowerModes.Suspend:
                Logger.Info("系统进入休眠状态");
                // 如果我们检测到系统休眠，也标记为睡眠状态
                if (GetStatus() == SleepStatus.Wake)
                {
                    SetStatus(SleepStatus.Sleep);
                }
                break;
                
            case PowerModes.Resume:
                Logger.Info("系统从休眠恢复");
                // 系统恢复时，标记为唤醒状态
                if (GetStatus() == SleepStatus.Sleep)
                {
                    UpdateActivityTime();  // 重置活动时间
                    SetStatus(SleepStatus.Wake);
                }
                break;
        }
    }

    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        if (e.Reason == SessionSwitchReason.RemoteDisconnect || 
            e.Reason == SessionSwitchReason.ConsoleDisconnect)
        {
            Logger.Warn("会话切换: 远程桌面连接断开");
            if (GetStatus() == SleepStatus.Wake)
            {
                SetStatus(SleepStatus.Sleep);
            }
        }

        else if (e.Reason == SessionSwitchReason.RemoteConnect || 
                 e.Reason == SessionSwitchReason.ConsoleConnect)
        {
            Logger.Info("会话切换: 远程桌面连接建立");
            if (GetStatus() == SleepStatus.Sleep)
            {
                SetStatus(SleepStatus.Wake);
            }
        }
    }

    #endregion

    #region 活动检测与状态管理

    private void StartActivityTimer()
    {
        StopActivityTimer();
        
        var timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(ActivityCheckInterval)
        };
        
        timer.Tick += (s, e) => CheckActivityStatus();
        timer.Start();
    }

    private void StopActivityTimer()
    {
        
    }

    private void CheckActivityStatus()
    {
        // 只在唤醒状态下检查是否应该进入睡眠
        if (GetStatus() != SleepStatus.Wake) 
            return;
        
        if (ShouldEnterSleep())
        {
            SetStatus(SleepStatus.Sleep);
        }
    }

   
    private bool ShouldEnterSleep()
    {
        // 检查用户是否长时间无操作
        var idleTime = DateTime.Now - GetLastActivityTime();
        if (idleTime.TotalMinutes < InactiveThresholdMinutes)
            return false;
        
        // 检查是否在播放声音
        if (Win32API.IsWindowsPlayingSound())
        {
            // 首次检测到有声音，记录开始时间
            if (!GetSoundStartTime().HasValue)
            {
                SetSoundStartTime(DateTime.Now);
                return false;
            }
            
            // 检查声音持续时间
            var soundDuration = DateTime.Now - GetSoundStartTime().Value;
            if (soundDuration.TotalHours < MaxSoundDurationHours)
                return false;
            
            // 声音持续超过阈值，重置计时器
            SetSoundStartTime(DateTime.MinValue);
        }
        else
        {
            // 没有声音，重置声音计时器
            SetSoundStartTime(DateTime.MinValue);
        }
        
        return true;  // 无操作且无声音，可以进入睡眠
    }

    private void CleanupHooks()
    {
        if (_keyboardHookId != nint.Zero)
        {
            Win32API.UnhookWindowsHookEx(_keyboardHookId);
            _keyboardHookId = nint.Zero;
        }
        
        if (_mouseHookId != nint.Zero)
        {
            Win32API.UnhookWindowsHookEx(_mouseHookId);
            _mouseHookId = nint.Zero;
        }
    }

    #endregion
}
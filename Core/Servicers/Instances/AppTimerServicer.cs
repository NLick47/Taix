using System.Diagnostics;
using System.Timers;
using SharedLibrary.Enums;
using SharedLibrary.Event;
using SharedLibrary.Models.AppObserver;
using SharedLibrary.Servicers;
using Timer = System.Timers.Timer;

namespace Core.Servicers.Instances;

public class AppTimerServicer : IAppTimerServicer
{
    private readonly IAppObserver _appObserver;
    private string _activeProcess;

    private Dictionary<string, AppData> _appData;
    private int _appDuration;
    private DateTime _endTime = DateTime.MinValue;

    private bool _isStart;
    private AppDurationUpdatedEventArgs _lastInvokeEventArgs;
    private DateTime _startTime = DateTime.MinValue;
    private Timer _timer;

    public AppTimerServicer(IAppObserver appObserver_)
    {
        _appObserver = appObserver_;
    }

    public event AppTimerEventHandler OnAppDurationUpdated;


    public void Start()
    {
        if (_isStart) return;

        Init();

        _isStart = true;
        _appObserver.OnAppActiveChanged += AppObserver_OnAppActiveChanged;
    }


    public void Stop()
    {
        _isStart = false;
        StopTimer();
        _appObserver.OnAppActiveChanged -= AppObserver_OnAppActiveChanged;
    }

    public AppDurationUpdatedEventArgs GetAppDuration()
    {
        if (_appDuration > 0 && !string.IsNullOrEmpty(_activeProcess) && _appData.ContainsKey(_activeProcess))
        {
            var data = _appData[_activeProcess];
            var args = new AppDurationUpdatedEventArgs(_appDuration, data.App, data.Window, _startTime, _endTime);
            return args;
        }

        return null;
    }

    private void Init()
    {
        _appData = new Dictionary<string, AppData>();
        _appDuration = 0;
        _activeProcess = string.Empty;

        _timer = new Timer();
        _timer.Interval = 1000;
        _timer.Elapsed += Timer_Elapsed;
    }

    private void AppObserver_OnAppActiveChanged(object sender, AppActiveChangedEventArgs e)
    {
        var processName = e.App.Process;
        var isStatistical = IsStatistical(e.App);

        Debug.WriteLine(processName + " -> " + _activeProcess);
        if (processName != _activeProcess)
        {
            StopTimer();
            InvokeEvent();

            if (isStatistical) StartTimer();

            _activeProcess = e.App.Type == AppType.SystemComponent ? string.Empty : processName;
        }

        if (isStatistical)
        {
            var data = new AppData
            {
                App = e.App,
                Window = e.Window
            };
            _appData[processName] = data;
        }
    }

    private void Timer_Elapsed(object sender, ElapsedEventArgs e)
    {
        _appDuration++;
    }

    private void StartTimer()
    {
        _timer.Start();
        _startTime = DateTime.Now;
        _appDuration = 0;
    }

    private void StopTimer()
    {
        _timer?.Stop();
        _endTime = DateTime.Now;
    }

    private void InvokeEvent()
    {
        var info = GetAppDuration();
        if (info != null)
        {
            if (_lastInvokeEventArgs?.ActiveTime.ToString() != _startTime.ToString() ||
                _lastInvokeEventArgs?.EndTime.ToString() != _endTime.ToString())
            {
                Debug.WriteLine("【计时更新】" + info);
                OnAppDurationUpdated?.Invoke(this, info);
            }
            else
            {
                Debug.WriteLine("【重复！！】" + _lastInvokeEventArgs + "，【now】" + info);
            }

            _lastInvokeEventArgs = info;
        }
    }

    /// <summary>
    ///     判断应用是否需要被统计
    /// </summary>
    /// <param name="app_"></param>
    /// <returns>需要统计时返回 true </returns>
    private bool IsStatistical(AppInfo app_)
    {
        return !string.IsNullOrEmpty(app_.Process) && app_.Type != AppType.SystemComponent &&
               !string.IsNullOrEmpty(app_.ExecutablePath);
    }

    public struct AppData
    {
        public AppInfo App { get; set; }
        public WindowInfo Window { get; set; }
    }
}
using Core.Event;
using Core.Servicers.Interfaces;
using SharedLibrary.Librarys;
using Timer = System.Timers.Timer;

namespace Core.Servicers.Instances;

public class DateTimeObserver : IDateTimeObserver
{
    private Timer timer;
    public event DateTimeObserverEventHandler OnDateTimeChanging;

    public void Start()
    {
        Stop();
        SetTimer();
    }

    public void Stop()
    {
        if (timer != null && timer.Enabled)
        {
            timer.Stop();
            timer.Dispose();
        }
    }

    private void SetTimer()
    {
        timer = new Timer();
        timer.Elapsed += Timer_Tick;
        var now = DateTime.Now;
        var updateTime = new DateTime(now.Year, now.Month, now.Day, now.Hour, 59, 59);
        var diffMilliseconds = updateTime.Subtract(now).TotalMilliseconds;
        if (diffMilliseconds < 0) diffMilliseconds = 1;
        timer.Interval = diffMilliseconds;
        timer.Start();
        Logger.Info("datetime observer start,interval(s):" + updateTime.Subtract(now).TotalSeconds);
    }

    private void Timer_Tick(object sender, EventArgs e)
    {
        timer.Stop();

        var nowTime = DateTime.Now;
        OnDateTimeChanging?.Invoke(this, nowTime);

        Thread.Sleep(2000);
        SetTimer();
    }
}
using System;
using Avalonia.Threading;

namespace Taix.Client.Foundation.Rx;


public sealed class AvaloniaContextScheduler : IScheduler
{
    public static readonly AvaloniaContextScheduler Instance = new();

    private AvaloniaContextScheduler() { }

    public void Schedule(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        Dispatcher.UIThread.Post(() => action(), DispatcherPriority.Normal);
    }

    public void Schedule(Action action, TimeSpan dueTime)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (dueTime <= TimeSpan.Zero)
        {
            Schedule(action);
            return;
        }

        var timer = new System.Timers.Timer(dueTime.TotalMilliseconds) { AutoReset = false };
        timer.Elapsed += (_, _) =>
        {
            timer.Dispose();
            Dispatcher.UIThread.Post(() => action(), DispatcherPriority.Normal);
        };
        timer.Start();
    }
}

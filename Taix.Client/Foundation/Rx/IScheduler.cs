using System;

namespace Taix.Client.Foundation.Rx;

public interface IScheduler
{
    void Schedule(Action action);
    void Schedule(Action action, TimeSpan dueTime);
}

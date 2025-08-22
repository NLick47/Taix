using SharedLibrary.Event;
using SharedLibrary.Servicers;

namespace Linux;

public class XSleepdiscover : ISleepdiscover
{
    public event SleepdiscoverEventHandler SleepStatusChanged;

    public void Start()
    {
    }

    public void Stop()
    {
    }
}
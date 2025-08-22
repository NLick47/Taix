using Core.Event;
using Core.Models.WebPage;
using WebSocketSharp.Server;

namespace Core.Librarys.Browser;

public class WebSocketEvent : WebSocketBehavior
{
    public static event WebServerEventHandler OnWebLog;

    public static void Invoke(NotifyWeb args_)
    {
        OnWebLog?.Invoke(args_);
    }
}
using Core.Librarys.Browser;
using Core.Models.WebPage;
using Core.Servicers.Interfaces;
using Newtonsoft.Json;
using WebSocketSharp;
using WebSocketSharp.Server;
using Logger = SharedLibrary.Librarys.Logger;

namespace Core.Servicers.Instances;

public class WebServer : WebSocketBehavior, IWebServer
{
    private bool _isStart;
    private WebSocketServer _webSocket;

    public void Start()
    {
        if (_isStart) return;
        try
        {
            _webSocket = new WebSocketServer(8908, false);
            _webSocket.AddWebSocketService<WebServer>("/TaiWebSentry");
            _webSocket.Start();
            _isStart = true;
        }
        catch (Exception ex)
        {
            Logger.Error("无法启动浏览器服务，" + ex);
        }
    }

    public void Stop()
    {
        if (!_isStart) return;
        _webSocket?.Stop();
        _isStart = false;
    }

    public void SendMsg(string msg_)
    {
        try
        {
            if (!_isStart) return;

            _webSocket.WebSocketServices.Broadcast(msg_);
        }
        catch (Exception ec)
        {
            Logger.Error(ec.ToString());
        }
    }


    protected override void OnMessage(MessageEventArgs e)
    {
        try
        {
            var log = JsonConvert.DeserializeObject<NotifyWeb>(e.Data);
            WebSocketEvent.Invoke(log);
        }
        catch
        {
        }
    }
}
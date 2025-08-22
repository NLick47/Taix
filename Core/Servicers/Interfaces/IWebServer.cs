namespace Core.Servicers.Interfaces;

public interface IWebServer
{
    void Start();
    void Stop();
    void SendMsg(string msg_);
}
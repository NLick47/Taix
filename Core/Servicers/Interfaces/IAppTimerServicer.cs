using SharedLibrary.Event;

namespace Core.Servicers;

public interface IAppTimerServicer
{
    /// <summary>
    ///     当有应用的计时更新时发生
    /// </summary>
    event AppTimerEventHandler OnAppDurationUpdated;

    /// <summary>
    ///     启动计时服务
    /// </summary>
    void Start();

    /// <summary>
    ///     停止计时服务
    /// </summary>
    void Stop();

    /// <summary>
    ///     获取当前应用的计时信息
    /// </summary>
    /// <returns></returns>
    AppDurationUpdatedEventArgs GetAppDuration();
}
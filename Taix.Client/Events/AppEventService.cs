using System;
using System.Threading;
using Taix.Client.Foundation.Rx;
using Taix.Client.Shared.Models;
using Taix.Client.Shared.Models.Web;

namespace Taix.Client.Events;

public class AppEventService : IAppEventService
{
    private readonly Subject<AppChangedEvent> _appChanged = new();
    private readonly Subject<WebSiteChangedEvent> _webSiteChanged = new();
    private long _changeVersion;

    public IObservable<AppChangedEvent> AppChanged => _appChanged.AsObservable();
    public IObservable<WebSiteChangedEvent> WebSiteChanged => _webSiteChanged.AsObservable();
    public long ChangeVersion => Interlocked.Read(ref _changeVersion);

    public void PublishAppChanged(AppModel app, AppChangeType changeType)
    {
        Interlocked.Increment(ref _changeVersion);
        _appChanged.OnNext(new AppChangedEvent(app, changeType));
    }

    public void PublishWebSiteChanged(WebSiteModel site, AppChangeType changeType)
    {
        Interlocked.Increment(ref _changeVersion);
        _webSiteChanged.OnNext(new WebSiteChangedEvent(site, changeType));
    }
}

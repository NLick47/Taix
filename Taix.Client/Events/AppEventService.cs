using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Taix.Client.Shared.Models;
using Taix.Client.Shared.Models.Web;

namespace Taix.Client.Events;

public class AppEventService : IAppEventService
{
    private readonly Subject<AppChangedEvent> _appChanged = new();
    private readonly Subject<WebSiteChangedEvent> _webSiteChanged = new();

    public IObservable<AppChangedEvent> AppChanged => _appChanged.AsObservable();
    public IObservable<WebSiteChangedEvent> WebSiteChanged => _webSiteChanged.AsObservable();

    public void PublishAppChanged(AppModel app, AppChangeType changeType)
    {
        _appChanged.OnNext(new AppChangedEvent(app, changeType));
    }

    public void PublishWebSiteChanged(WebSiteModel site, AppChangeType changeType)
    {
        _webSiteChanged.OnNext(new WebSiteChangedEvent(site, changeType));
    }
}
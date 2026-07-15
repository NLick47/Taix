using System;
using Taix.Client.Shared.Models;
using Taix.Client.Shared.Models.Web;

namespace Taix.Client.Events;

public interface IAppEventService
{
    IObservable<AppChangedEvent> AppChanged { get; }
    IObservable<WebSiteChangedEvent> WebSiteChanged { get; }
    
    void PublishAppChanged(AppModel app, AppChangeType changeType);
    void PublishWebSiteChanged(WebSiteModel site, AppChangeType changeType);
}
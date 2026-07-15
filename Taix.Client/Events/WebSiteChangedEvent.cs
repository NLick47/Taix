using Taix.Client.Shared.Models.Web;

namespace Taix.Client.Events;

public record WebSiteChangedEvent(WebSiteModel WebSite, AppChangeType ChangeType);
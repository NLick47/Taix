using Taix.Client.Shared.Models;

namespace Taix.Client.Events;

public record AppChangedEvent(AppModel App, AppChangeType ChangeType);
using System;
using Taix.Client.Shared.Models.Config;

namespace Taix.Client.Shared.Event;

public delegate void AppConfigEventHandler(ConfigModel oldConfig, ConfigModel newConfig);

public delegate void DateTimeObserverEventHandler(object sender, DateTime e);
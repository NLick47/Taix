using Core.Models.Config;

namespace Core.Event;

public delegate void AppConfigEventHandler(ConfigModel oldConfig, ConfigModel newConfig);

public delegate void DateTimeObserverEventHandler(object sender, DateTime e);
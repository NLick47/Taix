using SharedLibrary.Enums;

namespace SharedLibrary.Event;

public delegate void SleepdiscoverEventHandler(SleepStatus sleepStatus);

public delegate void DateTimeObserverEventHandler(object sender, DateTime e);
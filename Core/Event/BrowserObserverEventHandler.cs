using Core.Models.WebPage;
using SharedLibrary.Enums;

namespace Core.Event;

public delegate void BrowserObserverEventHandler(BrowserType browserType, Site site);
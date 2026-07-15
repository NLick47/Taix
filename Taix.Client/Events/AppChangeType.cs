using System;

namespace Taix.Client.Events;

[Flags]
public enum AppChangeType
{
    None = 0,
    Category = 1 << 0,
    Alias = 1 << 1,
    Description = 1 << 2,
    WebSiteCategory = 1 << 3,
    All = Category | Alias | Description | WebSiteCategory
}
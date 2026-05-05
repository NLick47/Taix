using System;
using System.Collections;
using System.Collections.Generic;
using Taix.Client.Shared.Models.Config;
using Taix.Client.Shared.Models.Config.Link;

namespace Taix.Client.Controls.SettingPanel;

internal static class AotTypeFactory
{
    public static object CreateInstance(Type type)
    {
        if (type == typeof(GeneralModel)) return new GeneralModel();
        if (type == typeof(BehaviorModel)) return new BehaviorModel();
        if (type == typeof(LinkModel)) return new LinkModel();
        throw new NotSupportedException($"Type {type.FullName} is not supported for AOT-safe instance creation.");
    }

    public static IList CreateList(Type elementType)
    {
        if (elementType == typeof(LinkModel)) return new List<LinkModel>();
        throw new NotSupportedException($"List<{elementType.Name}> is not supported for AOT-safe list creation.");
    }
}

using System;
using System.Collections;
using Taix.Client.Shared.Models.Config;

namespace Taix.Client.Controls.SettingPanel;

internal static class AotTypeFactory
{
    public static object CreateInstance(Type type)
    {
        if (type == typeof(GeneralModel)) return new GeneralModel();
        if (type == typeof(BehaviorModel)) return new BehaviorModel();
        throw new NotSupportedException($"Type {type.FullName} is not supported for AOT-safe instance creation.");
    }

    public static IList CreateList(Type elementType)
    {
        throw new NotSupportedException($"List<{elementType.Name}> is not supported for AOT-safe list creation.");
    }
}

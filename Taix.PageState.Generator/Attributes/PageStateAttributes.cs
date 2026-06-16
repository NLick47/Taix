using System;

namespace Taix.PageState.Generator.Attributes;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class GeneratePageStateAttribute : Attribute
{
}

/// <summary>
/// 标记一个属性需要被持久化到页面状态中
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
public sealed class PageStateAttribute : Attribute
{
    /// <summary>
    /// 指定从哪个集合属性中查找还原值
    /// 用于复杂类型（如 SelectItemModel）的还原
    /// 例如: LookupFrom = nameof(PeriodOptions)
    /// </summary>
    public string? LookupFrom { get; set; }

    /// <summary>
    /// 指定用于匹配查找的属性名，默认为 "Id"
    /// </summary>
    public string LookupBy { get; set; } = "Id";

    /// <summary>
    /// 标记此属性为数据缓存类型
    /// 数据缓存类型会单独存储，适用于大量数据
    /// </summary>
    public bool DataCache { get; set; }
}

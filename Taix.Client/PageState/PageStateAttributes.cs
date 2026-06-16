// ==================================================================
// 页面状态特性定义
// 用于标记需要状态持久化的 ViewModel 和属性
// ==================================================================

using System;

namespace Taix.Client.PageState;

/// <summary>
/// 标记一个 ViewModel 类需要生成页面状态持久化代码。
/// 被标记的类必须是 partial class。
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class GeneratePageStateAttribute : Attribute
{
}

/// <summary>
/// 标记一个属性需要被持久化到页面状态中。
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
public sealed class PageStateAttribute : Attribute
{
    /// <summary>
    /// 指定从哪个集合属性中查找还原值。
    /// 用于复杂类型（如 SelectItemModel）的还原。
    /// 例如: LookupFrom = nameof(PeriodOptions)
    /// </summary>
    public string? LookupFrom { get; set; }

    /// <summary>
    /// 指定用于匹配查找的属性名，默认为 "Id"。
    /// </summary>
    public string LookupBy { get; set; } = "Id";

    /// <summary>
    /// 标记此属性为数据缓存类型。
    /// 数据缓存类型会单独存储，适用于大量数据（如列表）。
    /// </summary>
    public bool DataCache { get; set; }
}

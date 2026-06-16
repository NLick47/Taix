// ==================================================================
// 状态服务接口定义
// ==================================================================

using System;

namespace Taix.Client.Servicers.Interfaces;

/// <summary>
/// 状态服务接口 - 用于存储和恢复页面状态
/// </summary>
public interface IStateService
{
    /// <summary>
    /// 获取状态
    /// </summary>
    /// <typeparam name="TKey">键类型</typeparam>
    /// <typeparam name="TState">状态类型</typeparam>
    /// <param name="key">键</param>
    /// <returns>状态值，不存在则返回 null</returns>
    TState? Get<TKey, TState>(TKey key) where TState : class;

    /// <summary>
    /// 设置状态
    /// </summary>
    /// <typeparam name="TKey">键类型</typeparam>
    /// <typeparam name="TState">状态类型</typeparam>
    /// <param name="key">键</param>
    /// <param name="state">状态值</param>
    void Set<TKey, TState>(TKey key, TState state) where TState : class;

    /// <summary>
    /// 检查状态是否存在
    /// </summary>
    /// <typeparam name="TKey">键类型</typeparam>
    /// <param name="key">键</param>
    /// <returns>是否存在</returns>
    bool HasState<TKey>(TKey key);

    /// <summary>
    /// 移除状态
    /// </summary>
    /// <typeparam name="TKey">键类型</typeparam>
    /// <param name="key">键</param>
    void Remove<TKey>(TKey key);

    /// <summary>
    /// 获取数据缓存
    /// </summary>
    /// <typeparam name="TKey">键类型</typeparam>
    /// <typeparam name="TData">数据类型</typeparam>
    /// <param name="key">键</param>
    /// <returns>数据值，不存在则返回 null</returns>
    TData? GetData<TKey, TData>(TKey key) where TData : class;

    /// <summary>
    /// 设置数据缓存
    /// </summary>
    /// <typeparam name="TKey">键类型</typeparam>
    /// <typeparam name="TData">数据类型</typeparam>
    /// <param name="key">键</param>
    /// <param name="data">数据值</param>
    void SetData<TKey, TData>(TKey key, TData data) where TData : class;

    /// <summary>
    /// 清除所有状态和数据缓存
    /// </summary>
    void Clear();
}

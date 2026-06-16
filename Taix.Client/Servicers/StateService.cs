// ==================================================================
// 状态服务实现
// ==================================================================

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Taix.Client.Servicers.Interfaces;

namespace Taix.Client.Servicers;

/// <summary>
/// 状态服务实现 - 使用并发字典存储状态
/// </summary>
public sealed class StateService : IStateService
{
    /// <summary>
    /// 状态存储字典，使用复合键（类型+键）
    /// </summary>
    private readonly ConcurrentDictionary<string, object> _stateStorage = new();

    public TState? Get<TKey, TState>(TKey key) where TState : class
    {
        var compositeKey = GetCompositeKey<TKey, TState>(key);
        if (_stateStorage.TryGetValue(compositeKey, out var value))
        {
            return value as TState;
        }
        return null;
    }

    public void Set<TKey, TState>(TKey key, TState state) where TState : class
    {
        var compositeKey = GetCompositeKey<TKey, TState>(key);
        _stateStorage[compositeKey] = state ?? throw new ArgumentNullException(nameof(state));
    }

    public bool HasState<TKey>(TKey key)
    {
        // 检查是否有任何以该键开头的状态
        var keyPrefix = $"{typeof(TKey).FullName}_{key}_";
        foreach (var kvp in _stateStorage)
        {
            if (kvp.Key.StartsWith(keyPrefix))
            {
                return true;
            }
        }
        return false;
    }

    public void Remove<TKey>(TKey key)
    {
        // 移除所有以该键开头的状态
        var keyPrefix = $"{typeof(TKey).FullName}_{key}_";
        var keysToRemove = new List<string>();

        foreach (var kvp in _stateStorage)
        {
            if (kvp.Key.StartsWith(keyPrefix))
            {
                keysToRemove.Add(kvp.Key);
            }
        }

        foreach (var k in keysToRemove)
        {
            _stateStorage.TryRemove(k, out _);
        }
    }

    public TData? GetData<TKey, TData>(TKey key) where TData : class
    {
        var compositeKey = GetDataKey<TKey, TData>(key);
        if (_stateStorage.TryGetValue(compositeKey, out var value))
        {
            return value as TData;
        }
        return null;
    }

    public void SetData<TKey, TData>(TKey key, TData data) where TData : class
    {
        var compositeKey = GetDataKey<TKey, TData>(key);
        _stateStorage[compositeKey] = data ?? throw new ArgumentNullException(nameof(data));
    }

    public void Clear()
    {
        _stateStorage.Clear();
    }

    /// <summary>
    /// 生成复合键用于状态存储
    /// </summary>
    private static string GetCompositeKey<TKey, TState>(TKey key)
    {
        return $"{typeof(TKey).FullName}_{key}_{typeof(TState).FullName}";
    }

    /// <summary>
    /// 生成复合键用于数据缓存存储
    /// </summary>
    private static string GetDataKey<TKey, TData>(TKey key)
    {
        return $"DATA_{typeof(TKey).FullName}_{key}_{typeof(TData).FullName}";
    }
}

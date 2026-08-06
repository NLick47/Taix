using System;

namespace Taix.Client.Foundation.Rx;

/// <summary>
/// 表示无参数异步操作的结果，替代 System.Reactive.Unit。
/// </summary>
public readonly struct Unit : IEquatable<Unit>
{
    public static readonly Unit Default = default;

    public bool Equals(Unit other) => true;
    public override bool Equals(object? obj) => obj is Unit;
    public override int GetHashCode() => 0;
    public override string ToString() => "()";
    public static bool operator ==(Unit left, Unit right) => true;
    public static bool operator !=(Unit left, Unit right) => false;
}

using System;

namespace Taix.Client.Shared.Helpers;

/// <summary>
/// 通过委托实现 IDisposable，用于简化事件订阅的取消操作。
/// </summary>
public sealed class DisposableAction : IDisposable
{
    private Action? _disposeAction;

    public DisposableAction(Action disposeAction)
    {
        _disposeAction = disposeAction;
    }

    public void Dispose()
    {
        _disposeAction?.Invoke();
        _disposeAction = null;
    }
}

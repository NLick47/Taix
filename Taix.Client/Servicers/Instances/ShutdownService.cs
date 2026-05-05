using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Taix.Client.Shared.Servicers.Interfaces;

namespace Taix.Client.Servicers.Instances;

public class ShutdownService : IShutdownService
{
    private readonly List<Func<Task>> _handlers = [];
    private bool _isShuttingDown;

    public void AddHandler(Func<Task> handler)
    {
        _handlers.Add(handler);
    }

    public async Task ShutdownAsync()
    {
        if (_isShuttingDown) return;
        _isShuttingDown = true;

        foreach (var handler in _handlers)
        {
            try
            {
                await handler();
            }
            catch
            {
                // 单个 handler 失败不影响其他 handler 执行
            }
        }
    }
}

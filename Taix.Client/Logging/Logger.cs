using System;

namespace Taix.Client.Logging;

public static class Logger
{
    private static ILogger? _impl;

    public static void SetLogger(ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _impl = logger;
    }

    public static bool IsConfigured => _impl is not null;

    [System.Diagnostics.Conditional("DEBUG")]
    public static void Debug(string message) => _impl?.Debug(message);

    public static void Info(string message) => _impl?.Info(message);

    public static void Warn(string message) => _impl?.Warn(message);

    public static void Error(string message, Exception? exception = null) => _impl?.Error(message, exception);

    public static void Flush() => _impl?.Flush();
}

using System;

namespace Taix.Client.Logging;

public interface ILogger
{
    void Debug(string message);
    void Info(string message);
    void Warn(string message);
    void Error(string message, Exception? exception = null);

    void Flush() { }
}

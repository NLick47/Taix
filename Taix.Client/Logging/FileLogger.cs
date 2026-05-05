using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Timer = System.Timers.Timer;

namespace Taix.Client.Logging;

// 文件日志实现，带队列缓冲和定时清理
public class FileLogger : ILogger, IDisposable
{
    private readonly FileLoggerOptions _options;
    private readonly ConcurrentQueue<string> _queue = new();
    private readonly Timer? _autoSaveTimer;
    private readonly object _writeLock = new();
    private DateTime _lastCleanupTime = DateTime.MinValue;

    public FileLogger(Action<FileLoggerOptions>? configure = null)
    {
        _options = new FileLoggerOptions();
        configure?.Invoke(_options);

        Task.Run(CleanupOldLogs);

        _autoSaveTimer = new Timer(_options.AutoSaveInterval);
        _autoSaveTimer.Elapsed += (_, _) => Flush();
        _autoSaveTimer.AutoReset = true;
        _autoSaveTimer.Start();
    }

    public void Debug(string message) => Enqueue("DEBUG", message);

    public void Info(string message) => Enqueue("INFO", message);

    public void Warn(string message) => Enqueue("WARN", message);
    


    public void Error(string message, Exception? exception = null)
    {
        var fullMessage = exception is not null
            ? $"{message}\r\nException: {exception}"
            : message;
        Enqueue("ERROR", fullMessage);
    }

    public void Flush()
    {
        var logs = new List<string>();
        while (_queue.TryDequeue(out var log))
        {
            logs.Add(log);
        }

        if (logs.Count == 0)
            return;

        try
        {
            lock (_writeLock)
            {
                var dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, _options.LogDirectory);
                Directory.CreateDirectory(dir);

                var file = Path.Combine(dir, $"taix-client-{DateTime.Now:yyyy-MM-dd}.log");
                var content = string.Join("", logs);

                if (!File.Exists(file))
                {
                    File.WriteAllText(file, $"# Log started at {DateTime.Now:yyyy-MM-dd HH:mm:ss}\r\n", Encoding.UTF8);
                }

                File.AppendAllText(file, content, Encoding.UTF8);

                if (_options.WriteToConsole)
                {
                    Console.Write(content);
                }
            }

            if ((DateTime.Now - _lastCleanupTime).TotalHours >= 24)
            {
                Task.Run(CleanupOldLogs);
            }
        }
        catch (Exception)
        {
            // ignored
        }
    }

    public void Dispose()
    {
        Flush();
        _autoSaveTimer?.Stop();
        _autoSaveTimer?.Dispose();
    }

    private void Enqueue(string level, string message)
    {
        var log = $"[{level}] {DateTime.Now:yyyy-MM-dd HH:mm:ss}\r\n{message}\r\n------------------------\r\n\r\n";
        _queue.Enqueue(log);

        if (_options.WriteToConsole)
        {
            Console.Write(log);
        }

        if (_queue.Count >= _options.SaveThreshold)
        {
            Task.Run(Flush);
        }
    }

    private void CleanupOldLogs()
    {
        try
        {
            _lastCleanupTime = DateTime.Now;
            var dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, _options.LogDirectory);
            if (!Directory.Exists(dir))
                return;

            var cutoff = DateTime.Now.AddDays(-_options.MaxLogFileAgeDays).Date;
            foreach (var file in Directory.GetFiles(dir, "*.log"))
            {
                try
                {
                    var name = Path.GetFileNameWithoutExtension(file);
                    if (DateTime.TryParseExact(name, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var date))
                    {
                        if (date < cutoff)
                            File.Delete(file);
                    }
                    else if (File.GetLastWriteTime(file) < cutoff)
                    {
                        File.Delete(file);
                    }
                }
                catch
                {
                    // ignore
                }
            }
        }
        catch (Exception)
        {
            // ignored
        }
    }
}

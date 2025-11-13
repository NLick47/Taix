using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Timer = System.Timers.Timer;

namespace SharedLibrary.Librarys;

public class LoggerOptions
{
    /// <summary>
    /// 日志保存阈值，达到此数量时自动保存
    /// </summary>
    public int SaveThreshold { get; set; } = 50;

    /// <summary>
    /// 自动保存间隔（毫秒）
    /// </summary>
    public double AutoSaveInterval { get; set; } = 1000 * 60 * 5; // 5分钟

    /// <summary>
    /// 日志文件最大保存天数
    /// </summary>
    public int MaxLogFileAgeDays { get; set; } = 30;

    /// <summary>
    /// 日志文件目录
    /// </summary>
    public string LogDirectory { get; set; } = "Log";

    /// <summary>
    /// 清理过期日志的频率（小时），0表示只在启动时清理
    /// </summary>
    public int CleanupIntervalHours { get; set; } = 0;
}

public static class Logger
{
    private static readonly LoggerOptions Options = new();
    private static readonly ConcurrentQueue<string> LogQueue = new();
    private static Timer? _autoSaveTimer;
    private static Timer? _cleanupTimer;
    private static readonly object WriteLock = new();
    private static volatile bool _isInitialized = false;
    private static DateTime _lastCleanupTime = DateTime.MinValue;

    /// <summary>
    /// 配置日志选项（应在应用程序启动时调用）
    /// </summary>
    public static void Configure(Action<LoggerOptions> configure)
    {
        if (_isInitialized)
            throw new InvalidOperationException("Logger已经初始化，配置必须在首次使用前进行");

        configure(Options);
        Initialize();
    }

    private static void Initialize()
    {
        if (_isInitialized) return;

        // 启动时清理过期日志
        Task.Run(() => CleanupOldLogs());

        // 自动保存计时器
        _autoSaveTimer = new Timer(Options.AutoSaveInterval);
        _autoSaveTimer.Elapsed += (s, e) => Save(true);
        _autoSaveTimer.Start();

        // 可选的定期清理计时器
        if (Options.CleanupIntervalHours > 0)
        {
            _cleanupTimer = new Timer(1000 * 60 * 60 * Options.CleanupIntervalHours);
            _cleanupTimer.Elapsed += (s, e) => CleanupOldLogs();
            _cleanupTimer.Start();
        }

        _isInitialized = true;

        // 应用程序退出时保存日志
        AppDomain.CurrentDomain.ProcessExit += (s, e) => 
        {
            Save(true);
            _autoSaveTimer?.Stop();
            _cleanupTimer?.Stop();
        };
        
        AppDomain.CurrentDomain.DomainUnload += (s, e) => 
        {
            Save(true);
            _autoSaveTimer?.Stop();
            _cleanupTimer?.Stop();
        };
    }

    // 确保在没有调用Configure时也能正常工作
    private static void EnsureInitialized()
    {
        if (!_isInitialized)
        {
            Initialize();
        }
    }

    public static void Info(string message, [CallerLineNumber] int callerLineNumber = -1,
        [CallerFilePath] string callerFilePath = null, [CallerMemberName] string callerMemberName = null)
    {
        EnsureInitialized();
        Log(LogLevel.Info, message, callerLineNumber, callerFilePath, callerMemberName);
    }

    public static void Warn(string message, [CallerLineNumber] int callerLineNumber = -1,
        [CallerFilePath] string callerFilePath = null, [CallerMemberName] string callerMemberName = null)
    {
        EnsureInitialized();
        Log(LogLevel.Warn, message, callerLineNumber, callerFilePath, callerMemberName);
    }

    public static void Error(string message, [CallerLineNumber] int callerLineNumber = -1,
        [CallerFilePath] string callerFilePath = null, [CallerMemberName] string callerMemberName = null)
    {
        EnsureInitialized();
        Log(LogLevel.Error, message, callerLineNumber, callerFilePath, callerMemberName);
    }

    public static void Error(Exception exception, string message = null, [CallerLineNumber] int callerLineNumber = -1,
        [CallerFilePath] string callerFilePath = null, [CallerMemberName] string callerMemberName = null)
    {
        EnsureInitialized();
        var exceptionMessage = $"{message}\r\nException: {exception}";
        Log(LogLevel.Error, exceptionMessage, callerLineNumber, callerFilePath, callerMemberName);
    }

    private static void Log(LogLevel level, string message, int lineNumber, string filePath, string memberName)
    {
        try
        {
            var callInfo = $"\r\nLine:{lineNumber}, File:{Path.GetFileName(filePath)}, Method:{memberName}";
            var formattedMessage = Format(level, message + callInfo);
            
            LogQueue.Enqueue(formattedMessage);

            // 定期清理检查（每天最多清理一次）
            if ((DateTime.Now - _lastCleanupTime).TotalHours >= 24)
            {
                Task.Run(() => CleanupOldLogs());
            }

            // 如果队列长度超过阈值，触发保存
            if (LogQueue.Count >= Options.SaveThreshold)
                Task.Run(() => Save(false));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Log failed: {ex}");
        }
    }

    private static string Format(LogLevel logLevel, string message)
    {
        message = HandleMessage(message);
        var logText = 
            $"[{logLevel}] {DateTime.Now:yyyy-MM-dd HH:mm:ss}\r\n{message}\r\n------------------------\r\n\r\n";
        
        Debug.WriteLine(logText);
        return logText;
    }

    /// <summary>
    /// 保存日志
    /// </summary>
    /// <param name="isNow">是否立即保存</param>
    public static void Save(bool isNow = false)
    {
        EnsureInitialized();
        
        if (!isNow && LogQueue.Count < Options.SaveThreshold) 
            return;

        try
        {
            var logsToSave = new List<string>();
            while (LogQueue.TryDequeue(out var logEntry))
            {
                logsToSave.Add(logEntry);
            }

            if (logsToSave.Count > 0)
            {
                WriteToFile(string.Join("", logsToSave));
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Save logs failed: {ex}");
        }
    }

    private static void WriteToFile(string logContent)
    {
        try
        {
            lock (WriteLock)
            {
                var logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Options.LogDirectory);
                var logFileName = Path.Combine(logDirectory, $"{DateTime.Now:yyyy-MM-dd}.log");
                
                var directory = Path.GetDirectoryName(logFileName);
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                if (!File.Exists(logFileName))
                {
                    InitializeLogFile(logFileName);
                }

                File.AppendAllText(logFileName, logContent, Encoding.UTF8);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Write to file failed: {ex}");
            // 将失败的日志重新加入队列
            foreach (var logEntry in logContent.Split(new[] { "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries))
            {
                LogQueue.Enqueue(logEntry + "\r\n\r\n");
            }
        }
    }

    private static void InitializeLogFile(string logFileName)
    {
        var clientInfo = new List<string>
        {
            FormatItem("Core Version", Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown"),
            FormatItem("OS Version", Environment.OSVersion.ToString()),
            FormatItem("Machine Name", Environment.MachineName),
            FormatItem("User Name", Environment.UserName),
            FormatItem(".NET Version", Environment.Version.ToString()),
            FormatItem("Process Architecture", Environment.Is64BitProcess ? "x64" : "x86"),
            FormatItem("Log Created", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")),
            "\r\n++++++++++++++++++++++++++++++++++++++++++++++++++\r\n\r\n"
        };

        File.WriteAllText(logFileName, string.Join("\r\n", clientInfo), Encoding.UTF8);
    }

    /// <summary>
    /// 清理过期日志文件
    /// </summary>
    private static void CleanupOldLogs()
    {
        try
        {
            _lastCleanupTime = DateTime.Now;
            
            var logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Options.LogDirectory);
            if (!Directory.Exists(logDirectory))
                return;

            var cutoffDate = DateTime.Now.AddDays(-Options.MaxLogFileAgeDays);
            var logFiles = Directory.GetFiles(logDirectory, "*.log");

            foreach (var logFile in logFiles)
            {
                try
                {
                    var fileInfo = new FileInfo(logFile);
                    
                    // 从文件名解析日期（格式：yyyy-MM-dd.log）
                    if (DateTime.TryParseExact(Path.GetFileNameWithoutExtension(logFile), 
                        "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var fileDate))
                    {
                        if (fileDate < cutoffDate.Date)
                        {
                            File.Delete(logFile);
                            Debug.WriteLine($"Deleted old log file: {logFile}");
                        }
                    }
                    else
                    {
                        // 如果无法从文件名解析日期，使用文件修改时间
                        if (fileInfo.LastWriteTime < cutoffDate)
                        {
                            File.Delete(logFile);
                            Debug.WriteLine($"Deleted old log file: {logFile}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Delete log file failed: {ex}");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Cleanup old logs failed: {ex}");
        }
    }

    /// <summary>
    /// 手动立即清理过期日志
    /// </summary>
    public static void CleanupNow()
    {
        EnsureInitialized();
        Task.Run(() => CleanupOldLogs());
    }

    private static string FormatItem(string name, string text)
    {
        return $"{name}: {text}";
    }

    private static string HandleMessage(string message)
    {
        return message?.Replace("\\u", "\\\\u") ?? string.Empty;
    }

    private enum LogLevel
    {
        Info,
        Warn,
        Error
    }
}
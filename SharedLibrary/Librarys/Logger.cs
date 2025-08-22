using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Timer = System.Timers.Timer;

namespace SharedLibrary.Librarys;

public static class Logger
{
    private const int threshold = 50;

    private static readonly object writeLock = new();

    private static readonly List<string> loggers = new();

    static Logger()
    {
        //  创建计时器，定时保存log
        var dispatcherTimer = new Timer(1000 * 60 * 5);
        dispatcherTimer.Elapsed += DispatcherTimer_Tick;
        dispatcherTimer.Start();
    }

    private static void DispatcherTimer_Tick(object sender, EventArgs e)
    {
        Save(true);
    }

    public static void Info(string message, [CallerLineNumber] int callerLineNumber = -1,
        [CallerFilePath] string callerFilePath = null, [CallerMemberName] string callerMemberName = null)
    {
        message = message + "\r\nLine:" + callerLineNumber + ",File:" + callerFilePath + ",name:" + callerMemberName;
        message = Fromat(LogLevel.Info, message);

        loggers.Add(message);
    }

    public static void Warn(string message, [CallerLineNumber] int callerLineNumber = -1,
        [CallerFilePath] string callerFilePath = null, [CallerMemberName] string callerMemberName = null)
    {
        message = message + "\r\nLine:" + callerLineNumber + ",File:" + callerFilePath + ",name:" + callerMemberName;
        message = Fromat(LogLevel.Warn, message);

        loggers.Add(message);
    }

    public static void Error(string message, [CallerLineNumber] int callerLineNumber = -1,
        [CallerFilePath] string callerFilePath = null, [CallerMemberName] string callerMemberName = null)
    {
        message = message + "\r\nLine:" + callerLineNumber + ",File:" + callerFilePath + ",name:" + callerMemberName;
        message = Fromat(LogLevel.Error, message);

        loggers.Add(message);
    }

    private static string Fromat(LogLevel logLevel, string message)
    {
        message = HandleMessage(message);
        var logText =
            $"[{logLevel}] {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}\r\n{message}\r\n------------------------\r\n\r\n";
        Debug.WriteLine(logText);
        return logText;
    }

    /// <summary>
    ///     将log写入文件
    /// </summary>
    /// <param name="isNow">是否强制立即写入</param>
    public static void Save(bool isNow = false)
    {
        if ((!isNow && loggers.Count < threshold) || loggers.Count == 0) return;

        try
        {
            Write(string.Join(string.Empty, loggers));
            loggers.Clear();
        }
        catch (Exception ec)
        {
            Error(ec.ToString());
        }
    }

    private static void Write(string log_)
    {
        try
        {
            lock (writeLock)
            {
                var loggerName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                    "Log", DateTime.Now.ToString("yyyy-MM-dd") + ".log");
                var dir = Path.GetDirectoryName(loggerName);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                if (!File.Exists(loggerName))
                {
                    List<string> clientInfo = new(5);
                    //  记录客户端信息
                    clientInfo.Add(FromatItem("Core Version",
                        Assembly.GetExecutingAssembly().GetName().Version.ToString()));
                    //clientInfo.Add(FromatItem("OS Name", SystemInfrastructure.GetWindowsVersionName()));
                    //clientInfo.Add(FromatItem("Computer Type", GetComputerType()));
                    //clientInfo.Add(FromatItem("Screen", GetScreenSize()));
                    clientInfo.Add("\r\n++++++++++++++++++++++++++++++++++++++++++++++++++\r\n\r\n");

                    File.WriteAllText(loggerName, string.Join("\r\n", clientInfo), Encoding.UTF8);
                }

                File.AppendAllText(loggerName, log_, Encoding.UTF8);
            }
        }
        catch (Exception ec)
        {
            loggers.Add(log_);
            Error(ec.ToString());
        }
    }


    //private static string GetScreenSize()
    //{
    //    return SystemInformation.VirtualScreen.Width + "*" + SystemInformation.VirtualScreen.Height;
    //}

    ///// <summary>
    ///// 获取电脑设备类型
    ///// </summary>
    ///// <returns>Desktop台式机，Laptop笔记本</returns>
    //private static string GetComputerType()
    //{
    //    if (SystemInformation.PowerStatus.BatteryChargeStatus == BatteryChargeStatus.NoSystemBattery)
    //    {
    //        return "Desktop";
    //    }
    //    else
    //    {
    //        return "Laptop";
    //    }
    //}
    private static string FromatItem(string name, string text)
    {
        return $"{name}:{text}";
    }

    private static string HandleMessage(string message)
    {
        message = message.Replace("\\u", "\\\\u");
        return message;
    }

    private enum LogLevel
    {
        Info,
        Warn,
        Error
    }
}
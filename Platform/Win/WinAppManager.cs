using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using SharedLibrary.Enums;
using SharedLibrary.Librarys;
using SharedLibrary.Models.AppObserver;
using SharedLibrary.Servicers;

namespace Win;

public class WinAppManager : IAppManager
{
    private static readonly HashSet<string> sysClassNameSet = new()
    {
        "Progman",
        "WorkerW",
        "Shell_TrayWnd",
        "XamlExplorerHostIslandWindow",
        "TopLevelWindowForOverflowXamlIsland",
        "Shell_InputSwitchTopLevelWindow",
        "LockScreenControllerProxyWindow",
        "ForegroundStaging",
        //  win7
        "DV2ControlHost",
        "Button"
    };

    private static readonly HashSet<string> sysProcessSet = new()
    {
        "ShellExperienceHost",
        "StartMenuExperienceHost",
        "SearchHost",
        "LockApp"
    };

    private readonly Dictionary<string, AppInfo> _apps;


    private readonly int _outTime = 5000;

    private readonly ConcurrentDictionary<int, (string Name, DateTime LastChecked)> _processNameCache;

    //private string _windowsVersionName;
    public WinAppManager()
    {
        _apps = new Dictionary<string, AppInfo>();
        _processNameCache = new ConcurrentDictionary<int, (string Name, DateTime LastChecked)>();
        //_windowsVersionName = SystemInfrastructure.GetWindowsVersionName();
    }

    public AppInfo GetAppInfo(nint hwnd_)
    {
        try
        {
            AppInfo app;
            GetWindowThreadProcessId(hwnd_, out var processId);
            var processName = GetAppProcessName(processId);
            var executablePath = string.Empty;
            var description = string.Empty;
            var appType = AppType.Win32;
            if (string.IsNullOrEmpty(processName)) return AppInfo.Empty;

            if (_apps.ContainsKey(processName)) return _apps[processName];

            if (processName == "ApplicationFrameHost")
            {
                //  uwp应用
                //  尝试直接获取可执行文件路径，如果为空表示刚启动或者全屏状态
                executablePath = GetUWPAPPExecutablePath(hwnd_, (uint)processId);
                appType = AppType.UWP;

                if (string.IsNullOrEmpty(executablePath))
                {
                    //  刚启动的程序需要延迟获取
                    var duration = 0;
                    while (processName == "ApplicationFrameHost")
                    {
                        Thread.Sleep(100);
                        duration += 100;
                        if (duration >= _outTime) break;
                        executablePath = GetUWPAPPExecutablePath(hwnd_, (uint)processId);

                        if (string.IsNullOrEmpty(executablePath))
                        {
                            //  可能是全屏状态
                            var pid = GetFullScreenUWPPID();
                            var name = GetAppProcessName(pid);

                            processName = string.IsNullOrEmpty(name) ? processName : name;
                            executablePath = GetAppExecutablePath(pid);
                            processId = pid;
                        }
                        else
                        {
                            processName = Path.GetFileNameWithoutExtension(executablePath);
                        }
                    }
                }
                else
                {
                    processName = Path.GetFileNameWithoutExtension(executablePath);
                }
            }
            //else if (_windowsVersionName.Contains("Windows 7") && processName == "dllhost")
            //{
            //    //  Windows7的COM应用

            //}
            else
            {
                executablePath = GetAppExecutablePath(processId);
            }

            if (!string.IsNullOrEmpty(executablePath))
            {
                var info = FileVersionInfo.GetVersionInfo(executablePath);
                description = info.FileDescription;
            }

            appType = IsSystemComponent(processName, hwnd_) ? AppType.SystemComponent : AppType.Win32;
            app = new AppInfo(hwnd_, processId, processName, description, executablePath, appType);
            if (processName != "explorer" && !_apps.ContainsKey(processName)) _apps.Add(processName, app);
            return app;
        }
        catch (Exception ex)
        {
            Logger.Error(ex.ToString());
            return AppInfo.Empty;
        }
    }

    private string? GetAppProcessName(int processId_)
    {
        if (!_processNameCache.TryGetValue(processId_, out var val) ||
            (DateTime.Now - val.LastChecked).TotalSeconds > 600)
        {
            using var process = Process.GetProcessById(processId_);
            if (!string.IsNullOrEmpty(process?.ProcessName))
                _processNameCache[processId_] = (process.ProcessName, DateTime.Now);
            return process?.ProcessName;
        }

        return _processNameCache[processId_].Name;
    }

    private string GetAppExecutablePath(int processId_)
    {
        var processHandle = nint.Zero;
        processHandle = OpenProcess(0x001F0FFF, false, processId_);
        var executablePath = string.Empty;
        if (processHandle != nint.Zero)
        {
            var MaxPathLength = 4096;
            var buffer = new StringBuilder(MaxPathLength);
            QueryFullProcessImageName(processHandle, 0, buffer, ref MaxPathLength);
            executablePath = buffer.ToString();
        }

        return executablePath;
    }

    private string GetUWPAPPExecutablePath(nint hWnd_, uint pID_)
    {
        var windowinfo = new WINDOWINFO();
        windowinfo.ownerpid = pID_;
        windowinfo.childpid = windowinfo.ownerpid;

        var pWindowinfo = Marshal.AllocHGlobal(Marshal.SizeOf(windowinfo));

        Marshal.StructureToPtr(windowinfo, pWindowinfo, false);

        var lpEnumFunc = new Win32API.EnumWindowProc(EnumChildWindowsCallback);
        Win32API.EnumChildWindows(hWnd_, lpEnumFunc, pWindowinfo);

        windowinfo = (WINDOWINFO)Marshal.PtrToStructure(pWindowinfo, typeof(WINDOWINFO));
        if (windowinfo.childpid == windowinfo.ownerpid) return null;
        nint proc;
        if ((proc = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, false, (int)windowinfo.childpid)) ==
            nint.Zero) return null;

        var capacity = 2000;
        var sb = new StringBuilder(capacity);
        QueryFullProcessImageName(proc, 0, sb, ref capacity);

        Marshal.FreeHGlobal(pWindowinfo);

        return sb.ToString(0, capacity);
    }

    private bool EnumChildWindowsCallback(nint hWnd_, nint lParam_)
    {
        var info = (WINDOWINFO)Marshal.PtrToStructure(lParam_, typeof(WINDOWINFO));

        int pID;
        GetWindowThreadProcessId(hWnd_, out pID);

        if (pID != info.ownerpid) info.childpid = (uint)pID;

        Marshal.StructureToPtr(info, lParam_, true);

        return true;
    }

    private nint getThreadWindowHandle(uint dwThreadId_)
    {
        nint hWnd;

        // Get Window Handle and title from Thread
        var guiThreadInfo = new GUITHREADINFO();
        guiThreadInfo.cbSize = (uint)Marshal.SizeOf(guiThreadInfo);

        GetGUIThreadInfo(dwThreadId_, ref guiThreadInfo);

        hWnd = guiThreadInfo.hwndFocus;
        //some times while changing the focus between different windows, it returns Zero so we would return the Active window in that case
        if (hWnd == nint.Zero) hWnd = guiThreadInfo.hwndActive;
        return hWnd;
    }

    #region 获取全屏UWP应用PID

    /// <summary>
    ///     获取全屏UWP应用PID
    /// </summary>
    /// <returns></returns>
    private int GetFullScreenUWPPID()
    {
        var current = getThreadWindowHandle(0);
        var processId = 0;
        GetWindowThreadProcessId(current, out processId);
        return processId;
    }

    #endregion


    #region 判断应用是否是系统组件

    private bool IsSystemComponent(string processName_, nint windowHandle_)
    {
        //  在windows7下，dllhost进程可能是Windows照片查看器，classname为：Photo_Lightweight_Viewer
        //  需要单独为windows7写一个判定器，然后加一个转换器，dllhost进程->windows照片查看器
        //  explorer，class:CabinetWClass在win7下可能是文件夹、控制面板、我的电脑属性
        bool isSys;

        if (processName_ == "explorer")
        {
            var className = Win32API.GetWindowClassName(windowHandle_);
            Logger.Info($"IsSystemComponent, process: {processName_}, className: {className}");

            isSys = !string.IsNullOrEmpty(className)
                ? sysClassNameSet.Contains(className)
                : Win32API.GetWindowTextLength(windowHandle_) <= 0;
        }
        else
        {
            isSys = sysProcessSet.Contains(processName_);
        }

        return isSys;
    }

    #endregion

    internal struct WINDOWINFO
    {
        public uint ownerpid;
        public uint childpid;
    }

    #region win32api

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern int GetWindowThreadProcessId(nint hwnd, out int ID);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern nint OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern bool QueryFullProcessImageName([In] nint hProcess, [In] int dwFlags,
        [Out] StringBuilder lpExeName, ref int lpdwSize);

    public const int PROCESS_QUERY_INFORMATION = 0x0400;
    public const int PROCESS_VM_READ = 0x0010;

    [DllImport("psapi.dll", SetLastError = true)]
    public static extern uint GetModuleBaseName(nint hProcess, nint hModule, [Out] StringBuilder lpBaseName,
        ref uint nSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern nint OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool CloseHandle(nint hObject);

    [StructLayout(LayoutKind.Sequential)]
    public struct GUITHREADINFO
    {
        public uint cbSize;
        public uint flags;
        public nint hwndActive;
        public nint hwndFocus;
        public nint hwndCapture;
        public nint hwndMenuOwner;
        public nint hwndMoveSize;
        public nint hwndCaret;
        public RECT rcCaret;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetGUIThreadInfo(uint idThread, ref GUITHREADINFO lpgui);

    #endregion
}
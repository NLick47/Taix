using SharedLibrary.Librarys;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Win
{
    public static class Win32API
    {
        [DllImport("user32.dll")]
        public static extern nint GetForegroundWindow();
        [DllImport("user32.dll")]
        public static extern nint SetForegroundWindow(nint hwnd);
        public delegate void WinEventDelegate(nint hWinEventHook, uint eventType,
          nint hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);
        [DllImport("user32.dll")]
        public static extern nint SetWinEventHook(uint eventMin, uint eventMax, nint
          hmodWinEventProc, WinEventDelegate lpfnWinEventProc, uint idProcess,
          uint idThread, uint dwFlags);

        [DllImport("user32.dll")]
        public static extern bool UnhookWinEvent(nint hWinEventHook);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int GetWindowThreadProcessId(nint hwnd, out int ID);

        [DllImport("psapi.dll", SetLastError = true)]
        public static extern int GetModuleFileNameExA(nint hProcess, nint hModule, StringBuilder lpFilename, int nSize);
        //[DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        //static extern bool QueryFullProcessImageName(IntPtr hProcess, uint dwFlags, [Out, MarshalAs(UnmanagedType.LPTStr)] StringBuilder lpExeName, ref uint lpdwSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern nint OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CloseHandle(nuint hObject);
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern int GetClassName(nint hWnd, StringBuilder lpClassName, int nMaxCount);


        internal struct WINDOWINFO
        {
            public uint ownerpid;
            public uint childpid;
        }

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);

        [DllImport("user32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EnumChildWindows(nint hWndParent, EnumWindowProc lpEnumFunc, nint lParam);



        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern bool QueryFullProcessImageName([In] nint hProcess, [In] int dwFlags, [Out] StringBuilder lpExeName, ref int lpdwSize);


        public delegate bool EnumWindowProc(nint hWnd, nint parameter);

        public const uint PROCESS_QUERY_INFORMATION = 0x400;
        public const uint PROCESS_VM_READ = 0x010;

        //private const uint WINEVENT_OUTOFCONTEXT = 0;
        //private const uint EVENT_SYSTEM_FOREGROUND = 3;
        public static string UWP_AppName(nint hWnd, uint pID)
        {
            WINDOWINFO windowinfo = new WINDOWINFO();
            windowinfo.ownerpid = pID;
            windowinfo.childpid = windowinfo.ownerpid;

            nint pWindowinfo = Marshal.AllocHGlobal(Marshal.SizeOf(windowinfo));

            Marshal.StructureToPtr(windowinfo, pWindowinfo, false);

            EnumWindowProc lpEnumFunc = new EnumWindowProc(EnumChildWindowsCallback);
            EnumChildWindows(hWnd, lpEnumFunc, pWindowinfo);

            windowinfo = (WINDOWINFO)Marshal.PtrToStructure(pWindowinfo, typeof(WINDOWINFO));
            if (windowinfo.childpid == windowinfo.ownerpid)
            {
                return null;
            }
            nint proc;
            if ((proc = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, false, (int)windowinfo.childpid)) == nint.Zero) return null;

            int capacity = 2000;
            StringBuilder sb = new StringBuilder(capacity);
            QueryFullProcessImageName(proc, 0, sb, ref capacity);

            Marshal.FreeHGlobal(pWindowinfo);

            return sb.ToString(0, capacity);
        }

        private static bool EnumChildWindowsCallback(nint hWnd, nint lParam)
        {
            WINDOWINFO info = (WINDOWINFO)Marshal.PtrToStructure(lParam, typeof(WINDOWINFO));

            uint pID;
            GetWindowThreadProcessId(hWnd, out pID);

            if (pID != info.ownerpid) info.childpid = pID;

            Marshal.StructureToPtr(info, lParam, true);

            return true;
        }


        /// <summary>
        /// 获取鼠标坐标
        /// </summary>
        /// <param name="lpPoint"></param>
        /// <returns></returns>
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;

            public static implicit operator Point(POINT point)
            {
                return new Point(point.X, point.Y);
            }
        }

        public static Point GetCursorPosition()
        {
            POINT lpPoint;
            GetCursorPos(out lpPoint);
            return lpPoint;
        }

        #region 声音判断
        /// <summary>
        /// 指示系统当前是否在播放声音
        /// </summary>
        /// <returns></returns>
        public static bool IsWindowsPlayingSound()
        {
            try
            {
                IMMDeviceEnumerator enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
                IMMDevice speakers = enumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia);
                IAudioMeterInformation meter = (IAudioMeterInformation)speakers.Activate(typeof(IAudioMeterInformation).GUID, 0, nint.Zero);
                if (meter != null)
                {

                    float value = meter.GetPeakValue();

                    // this is a bit tricky. 0 is the official "no sound" value
                    // but for example, if you open a video and plays/stops with it (w/o killing the app/window/stream),
                    // the value will not be zero, but something really small (around 1E-09)
                    // so, depending on your context, it is up to you to decide
                    // if you want to test for 0 or for a small value
                    return value > 1E-08;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception ec)
            {
                Logger.Error(ec.Message);
                return false;
            }
        }

        [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
        private class MMDeviceEnumerator
        {
        }

        private enum EDataFlow
        {
            eRender,
            eCapture,
            eAll,
        }

        private enum ERole
        {
            eConsole,
            eMultimedia,
            eCommunications,
        }

        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
        private interface IMMDeviceEnumerator
        {
            void NotNeeded();
            IMMDevice GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role);
            // the rest is not defined/needed
        }

        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("D666063F-1587-4E43-81F1-B948E807363F")]
        private interface IMMDevice
        {
            [return: MarshalAs(UnmanagedType.IUnknown)]
            object Activate([MarshalAs(UnmanagedType.LPStruct)] Guid iid, int dwClsCtx, nint pActivationParams);
            // the rest is not defined/needed
        }

        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064")]
        private interface IAudioMeterInformation
        {
            float GetPeakValue();
            // the rest is not defined/needed
        }
        #endregion

        #region 键盘钩子
        [StructLayout(LayoutKind.Sequential)]
        public class KeyboardHookStruct
        {
            public int vkCode;  //定一个虚拟键码。该代码必须有一个价值的范围1至254
            public int scanCode; // 指定的硬件扫描码的关键
            public int flags;  // 键标志
            public int time; // 指定的时间戳记的这个讯息
            public int dwExtraInfo; // 指定额外信息相关的信息
        }

        private const int WH_KEYBOARD_LL = 13;
        public const int WM_KEYDOWN = 0x0100;
        private const int WH_MOUSE_LL = 14;
        public const int WM_LBUTTONDBLCLK = 0x202;
        public const int WM_WHEEL = 0x20a;
        public const int WM_KEYUP = 0x101;
        public const int WM_SYSKEYDOWN = 0x104;
        public const int WM_SYSKEYUP = 0x105;
        /// <summary>
        /// 设置键盘钩子
        /// </summary>
        /// <param name="proc"></param>
        /// <returns></returns>
        public static nint SetKeyboardHook(LowLevelKeyboardProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc,
                    GetModuleHandle(curModule.ModuleName), 0);
            }
        }


        /// <summary>
        /// 设置鼠标钩子
        /// </summary>
        /// <param name="proc"></param>
        /// <returns></returns>
        public static nint SetMouseHook(LowLevelKeyboardProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_MOUSE_LL, proc,
                    GetModuleHandle(curModule.ModuleName), 0);
            }
        }
        public delegate nint LowLevelKeyboardProc(
            int nCode, nint wParam, nint lParam);



        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern nint SetWindowsHookEx(int idHook,
            LowLevelKeyboardProc lpfn, nint hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnhookWindowsHookEx(nint hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern nint CallNextHookEx(nint hhk, int nCode,
            nint wParam, nint lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern nint GetModuleHandle(string lpModuleName);
        #endregion

        #region 获取窗口信息
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetWindowRect(nint hWnd, out RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;        // x position of upper-left corner
            public int Top;         // y position of upper-left corner
            public int Right;       // x position of lower-right corner
            public int Bottom;      // y position of lower-right corner

            public int Width
            {
                get
                {
                    return Right - Left;
                }
            }
            public int Height
            {
                get
                {
                    return Bottom - Top;
                }
            }

        }
        public static RECT GetWindowRect(nint handle_)
        {
            try
            {
                GetWindowRect(handle_, out RECT rect);
                return rect;

            }
            catch (Exception e)
            {
                return new RECT()
                {
                    Left = 0,
                    Bottom = 0,
                    Right = 0,
                    Top = 0
                };
            }
        }
        #endregion

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int GetWindowTextLength(nint hWnd);
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int GetWindowText(nint hWnd, StringBuilder lpString, int nMaxCount);
        public static string GetWindowClassName(nint handle_)
        {
            try
            {
                StringBuilder stringBuilder = new StringBuilder(256);
                GetClassName(handle_, stringBuilder, stringBuilder.Capacity);

                return stringBuilder.ToString();
            }
            catch (Exception e)
            {
                return string.Empty;
            }
        }
    }
}

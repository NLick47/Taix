using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Linux
{
    public static class X11
    {
        [DllImport("libX11.so", EntryPoint = "XOpenDisplay")]
        public static extern IntPtr OpenDisplay(IntPtr displayName);

        [DllImport("libX11.so", EntryPoint = "XDefaultRootWindow")]
        public static extern IntPtr DefaultRootWindow(IntPtr display);

        [DllImport("libX11.so", EntryPoint = "XSelectInput")]
        public static extern int SelectInput(IntPtr display, IntPtr window, EventMask event_mask);

        [DllImport("libX11.so", EntryPoint = "XPending")]
        public static extern int Pending(IntPtr display);

        [DllImport("libX11.so", EntryPoint = "XNextEvent")]
        public static extern int NextEvent(IntPtr display, out XEvent @event);

        [DllImport("libX11.so", EntryPoint = "XCloseDisplay")]
        public static extern int CloseDisplay(IntPtr display);
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct XEvent
    {
        public EventType type;
        private IntPtr serial; /* # of last request processed by server */
        private bool send_event; /* true if this came from a SendEvent request */
        private IntPtr display; /* Display the event was read from */
        private IntPtr window; /* "window" field in event */
    }

    public enum EventType : byte
    {
        FocusIn = 9,
        FocusOut = 10
    }

    [Flags]
    public enum EventMask : uint
    {
        NoEventMask = 0,
        KeyPressMask = (1U << 0),
        KeyReleaseMask = (1U << 1),
        ButtonPressMask = (1U << 2),
        ButtonReleaseMask = (1U << 3),
        EnterWindowMask = (1U << 4),
        LeaveWindowMask = (1U << 5),
        PointerMotionMask = (1U << 6),
        PointerMotionHintMask = (1U << 7),
        Button1MotionMask = (1U << 8),
        Button2MotionMask = (1U << 9),
        Button3MotionMask = (1U << 10),
        Button4MotionMask = (1U << 11),
        Button5MotionMask = (1U << 12),
        ButtonMotionMask = (1U << 13),
        KeymapStateMask = (1U << 14),
        ExposureMask = (1U << 15),
        VisibilityChangeMask = (1U << 16),
        StructureNotifyMask = (1U << 17),
        ResizeRedirectMask = (1U << 18),
        SubstructureNotifyMask = (1U << 19),
        SubstructureRedirectMask = (1U << 20),
        FocusChangeMask = (1U << 21),
        PropertyChangeMask = (1U << 22),
        ColormapChangeMask = (1U << 23),
        OwnerGrabButtonMask = (1U << 24)
    }
}

using System.Runtime.InteropServices;

namespace Linux;

internal static unsafe class Xlib
{
    private const string libX11 = "libX11.so.6";

    [DllImport(libX11)]
    public static extern IntPtr XOpenDisplay(IntPtr display);

    [DllImport(libX11)]
    public static extern int XCloseDisplay(IntPtr display);


    [DllImport(libX11, EntryPoint = "XDefaultRootWindow")]
    public static extern IntPtr DefaultRootWindow(IntPtr display);

    [DllImport(libX11)]
    public static extern IntPtr XRootWindow(IntPtr display, int screen_number);

    [DllImport(libX11)]
    public static extern int XDefaultScreen(IntPtr display);

    [DllImport(libX11, EntryPoint = "XPending")]
    public static extern int Pending(IntPtr display);

    [DllImport(libX11)]
    public static extern int XNextEvent(IntPtr display, IntPtr event_return);

    [DllImport(libX11, EntryPoint = "XCloseDisplay")]
    public static extern int CloseDisplay(IntPtr display);

    [DllImport(libX11)]
    public static extern int XPending(IntPtr diplay);

    [DllImport(libX11)]
    public static extern IntPtr XSelectInput(IntPtr display, IntPtr window, EventMask event_mask);

    [DllImport(libX11)]
    public static extern int XGetInputFocus(IntPtr display, out IntPtr window, out IntPtr revertToReturn);

    [DllImport(libX11)]
    public static extern IntPtr XInternAtom(IntPtr display, string atom_name, bool only_if_exists);

    [DllImport(libX11)]
    public static extern int XGetWindowProperty(IntPtr display, IntPtr window, IntPtr atom, IntPtr long_offset,
        IntPtr long_length, bool delete, IntPtr req_type, out IntPtr actual_type, out int actual_format,
        out IntPtr nitems, out IntPtr bytes_after, out IntPtr prop);

    [DllImport(libX11)]
    public static extern int XFree(IntPtr data);

    [DllImport(libX11)]
    public static extern int XFree(void* data);
}

internal enum Atom
{
    AnyPropertyType = 0,
    XA_PRIMARY = 1,
    XA_SECONDARY = 2,
    XA_ARC = 3,
    XA_ATOM = 4,
    XA_BITMAP = 5,
    XA_CARDINAL = 6,
    XA_COLORMAP = 7,
    XA_CURSOR = 8,
    XA_CUT_BUFFER0 = 9,
    XA_CUT_BUFFER1 = 10,
    XA_CUT_BUFFER2 = 11,
    XA_CUT_BUFFER3 = 12,
    XA_CUT_BUFFER4 = 13,
    XA_CUT_BUFFER5 = 14,
    XA_CUT_BUFFER6 = 15,
    XA_CUT_BUFFER7 = 16,
    XA_DRAWABLE = 17,
    XA_FONT = 18,
    XA_INTEGER = 19,
    XA_PIXMAP = 20,
    XA_POINT = 21,
    XA_RECTANGLE = 22,
    XA_RESOURCE_MANAGER = 23,
    XA_RGB_COLOR_MAP = 24,
    XA_RGB_BEST_MAP = 25,
    XA_RGB_BLUE_MAP = 26,
    XA_RGB_DEFAULT_MAP = 27,
    XA_RGB_GRAY_MAP = 28,
    XA_RGB_GREEN_MAP = 29,
    XA_RGB_RED_MAP = 30,
    XA_STRING = 31,
    XA_VISUALID = 32,
    XA_WINDOW = 33,
    XA_WM_COMMAND = 34,
    XA_WM_HINTS = 35,
    XA_WM_CLIENT_MACHINE = 36,
    XA_WM_ICON_NAME = 37,
    XA_WM_ICON_SIZE = 38,
    XA_WM_NAME = 39,
    XA_WM_NORMAL_HINTS = 40,
    XA_WM_SIZE_HINTS = 41,
    XA_WM_ZOOM_HINTS = 42,
    XA_MIN_SPACE = 43,
    XA_NORM_SPACE = 44,
    XA_MAX_SPACE = 45,
    XA_END_SPACE = 46,
    XA_SUPERSCRIPT_X = 47,
    XA_SUPERSCRIPT_Y = 48,
    XA_SUBSCRIPT_X = 49,
    XA_SUBSCRIPT_Y = 50,
    XA_UNDERLINE_POSITION = 51,
    XA_UNDERLINE_THICKNESS = 52,
    XA_STRIKEOUT_ASCENT = 53,
    XA_STRIKEOUT_DESCENT = 54,
    XA_ITALIC_ANGLE = 55,
    XA_X_HEIGHT = 56,
    XA_QUAD_WIDTH = 57,
    XA_WEIGHT = 58,
    XA_POINT_SIZE = 59,
    XA_RESOLUTION = 60,
    XA_COPYRIGHT = 61,
    XA_NOTICE = 62,
    XA_FONT_NAME = 63,
    XA_FAMILY_NAME = 64,
    XA_FULL_NAME = 65,
    XA_CAP_HEIGHT = 66,
    XA_WM_CLASS = 67,
    XA_WM_TRANSIENT_FOR = 68,

    XA_LAST_PREDEFINED = 68
}

[StructLayout(LayoutKind.Sequential)]
internal struct XEvent
{
    public Event type;
    private IntPtr serial;
    private bool send_event;
    private IntPtr display;
    private IntPtr window;
}

[StructLayout(LayoutKind.Sequential, Size = 24 * sizeof(long))]
internal struct XAnyEvent
{
    public int type;
    public ulong serial;
    public bool send_event;
    public IntPtr display;
    public IntPtr window;
}

internal enum EventMask : long
{
    NoEventMask = 0L,
    KeyPressMask = 1L << 0,
    KeyReleaseMask = 1L << 1,
    ButtonPressMask = 1L << 2,
    ButtonReleaseMask = 1L << 3,
    EnterWindowMask = 1L << 4,
    LeaveWindowMask = 1L << 5,
    PointerMotionMask = 1L << 6,
    PointerMotionHintMask = 1L << 7,
    Button1MotionMask = 1L << 8,
    Button2MotionMask = 1L << 9,
    Button3MotionMask = 1L << 10,
    Button4MotionMask = 1L << 11,
    Button5MotionMask = 1L << 12,
    ButtonMotionMask = 1L << 13,
    KeymapStateMask = 1L << 14,
    ExposureMask = 1L << 15,
    VisibilityChangeMask = 1L << 16,
    StructureNotifyMask = 1L << 17,
    ResizeRedirectMask = 1L << 18,
    SubstructureNotifyMask = 1L << 19,
    SubstructureRedirectMask = 1L << 20,
    FocusChangeMask = 1L << 21,
    PropertyChangeMask = 1L << 22,
    ColormapChangeMask = 1L << 23,
    OwnerGrabButtonMask = 1L << 24
}

internal enum Event
{
    KeyPress = 2,
    KeyRelease = 3,
    ButtonPress = 4,
    ButtonRelease = 5,
    MotionNotify = 6,
    EnterNotify = 7,
    LeaveNotify = 8,
    FocusIn = 9,
    FocusOut = 10,
    KeymapNotify = 11,
    Expose = 12,
    GraphicsExpose = 13,
    NoExpose = 14,
    VisibilityNotify = 15,
    CreateNotify = 16,
    DestroyNotify = 17,
    UnmapNotify = 18,
    MapNotify = 19,
    MapRequest = 20,
    ReparentNotify = 21,
    ConfigureNotify = 22,
    ConfigureRequest = 23,
    GravityNotify = 24,
    ResizeRequest = 25,
    CirculateNotify = 26,
    CirculateRequest = 27,
    PropertyNotify = 28,
    SelectionClear = 29,
    SelectionRequest = 30,
    SelectionNotify = 31,
    ColormapNotify = 32,
    ClientMessage = 33,
    MappingNotify = 34,
    GenericEvent = 35,
    LASTEvent = 36
}
using System.Runtime.InteropServices;

namespace WindowThumbWall;

internal static class NativeMethods
{
    // ── DWM Thumbnail ────────────────────────────────────────────

    [DllImport("dwmapi.dll")]
    internal static extern int DwmRegisterThumbnail(
        IntPtr hwndDestination, IntPtr hwndSource, out IntPtr phThumbnailId);

    [DllImport("dwmapi.dll")]
    internal static extern int DwmUnregisterThumbnail(IntPtr hThumbnailId);

    [DllImport("dwmapi.dll")]
    internal static extern int DwmUpdateThumbnailProperties(
        IntPtr hThumbnailId, ref DWM_THUMBNAIL_PROPERTIES ptnProperties);

    internal const uint DWM_TNP_RECTDESTINATION = 0x00000001;
    internal const uint DWM_TNP_VISIBLE         = 0x00000008;

    // ── Window enumeration & info ────────────────────────────────

    internal delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern int GetWindowText(IntPtr hWnd, char[] lpString, int nMaxCount);

    [DllImport("user32.dll")]
    internal static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    internal static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    internal const int GWL_EXSTYLE          = -20;
    internal const uint WS_EX_TOOLWINDOW    = 0x00000080;

    // ── Process identification ───────────────────────────────────

    [DllImport("user32.dll")]
    internal static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    internal static string GetProcessName(IntPtr hWnd)
    {
        GetWindowThreadProcessId(hWnd, out uint pid);
        if (pid == 0) return string.Empty;
        try
        {
            return System.Diagnostics.Process.GetProcessById((int)pid).ProcessName;
        }
        catch
        {
            return string.Empty;
        }
    }

    // ── Shell hook (flash / activation detection) ────────────────

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool RegisterShellHookWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DeregisterShellHookWindow(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern uint RegisterWindowMessage(string lpString);

    [DllImport("user32.dll")]
    internal static extern IntPtr GetForegroundWindow();

    internal const int HSHELL_WINDOWACTIVATED = 4;
    internal const int HSHELL_REDRAW          = 6;
    internal const int HSHELL_HIGHBIT         = 0x8000;
    internal const int HSHELL_FLASH           = HSHELL_REDRAW | HSHELL_HIGHBIT;

    // ── HWND creation (for ThumbHost) ────────────────────────────

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern IntPtr CreateWindowEx(
        uint dwExStyle, string lpClassName, string lpWindowName,
        uint dwStyle, int x, int y, int nWidth, int nHeight,
        IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    internal static extern IntPtr GetModuleHandle(string? lpModuleName);

    internal const uint WS_CHILD        = 0x40000000;
    internal const uint WS_CLIPCHILDREN = 0x02000000;

    // ── Coordinate helpers ───────────────────────────────────────

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

    // ── Structs ──────────────────────────────────────────────────

    [StructLayout(LayoutKind.Sequential)]
    internal struct DWM_THUMBNAIL_PROPERTIES
    {
        public uint dwFlags;
        public RECT rcDestination;
        public RECT rcSource;
        public byte opacity;
        [MarshalAs(UnmanagedType.Bool)]
        public bool fVisible;
        [MarshalAs(UnmanagedType.Bool)]
        public bool fSourceClientAreaOnly;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    internal struct POINT { public int X, Y; }

    // ── Helpers ──────────────────────────────────────────────────

    internal static string GetWindowTitle(IntPtr hWnd)
    {
        int len = GetWindowTextLength(hWnd);
        if (len == 0) return string.Empty;
        var buf = new char[len + 1];
        GetWindowText(hWnd, buf, buf.Length);
        return new string(buf, 0, len);
    }

    internal static bool IsAltTabWindow(IntPtr hWnd)
    {
        if (!IsWindowVisible(hWnd)) return false;
        nint exStyle = (nint)GetWindowLongPtr(hWnd, GWL_EXSTYLE);
        if ((exStyle & (nint)WS_EX_TOOLWINDOW) != 0) return false;
        return !string.IsNullOrWhiteSpace(GetWindowTitle(hWnd));
    }
}

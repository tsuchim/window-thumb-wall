using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Text;

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

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern int GetClassName(IntPtr hWnd, char[] lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    internal static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    internal static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

    internal const int GWL_EXSTYLE          = -20;
    internal const uint WS_EX_TOOLWINDOW    = 0x00000080;
    internal const uint GW_OWNER            = 4;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    internal const int SW_RESTORE = 9;

    // ── Process identification ───────────────────────────────────

    [DllImport("user32.dll")]
    internal static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern IntPtr OpenProcess(uint processAccess, bool inheritHandle, uint processId);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool QueryFullProcessImageName(
        IntPtr hProcess,
        uint dwFlags,
        StringBuilder lpExeName,
        ref int lpdwSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetCurrentPackageFullName(ref int packageFullNameLength, StringBuilder? packageFullName);

    internal const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
    private const int APPMODEL_ERROR_NO_PACKAGE = 15700;
    private const int ERROR_SUCCESS = 0;
    private const int ERROR_INSUFFICIENT_BUFFER = 122;

    internal static string GetProcessName(IntPtr hWnd)
    {
        GetWindowThreadProcessId(hWnd, out uint pid);
        if (pid == 0) return string.Empty;
        try
        {
            using var process = Process.GetProcessById((int)pid);
            return process.ProcessName;
        }
        catch
        {
            return string.Empty;
        }
    }

    internal static string GetAppDisplayName(IntPtr hWnd)
    {
        GetWindowThreadProcessId(hWnd, out uint pid);
        if (pid == 0) return string.Empty;

        try
        {
            using var process = Process.GetProcessById((int)pid);
            string? fileName = process.MainModule?.FileName;
            if (!string.IsNullOrWhiteSpace(fileName))
            {
                FileVersionInfo info = FileVersionInfo.GetVersionInfo(fileName);
                if (!string.IsNullOrWhiteSpace(info.ProductName))
                    return info.ProductName;
                if (!string.IsNullOrWhiteSpace(info.FileDescription))
                    return info.FileDescription;
            }

            return process.ProcessName;
        }
        catch
        {
            return string.Empty;
        }
    }

    internal static string GetProcessImagePath(IntPtr hWnd)
    {
        GetWindowThreadProcessId(hWnd, out uint pid);
        if (pid == 0)
            return string.Empty;

        IntPtr processHandle = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, inheritHandle: false, pid);
        if (processHandle == IntPtr.Zero)
            return string.Empty;

        try
        {
            StringBuilder buffer = new(1024);
            int length = buffer.Capacity;
            return QueryFullProcessImageName(processHandle, 0, buffer, ref length)
                ? buffer.ToString(0, length)
                : string.Empty;
        }
        finally
        {
            CloseHandle(processHandle);
        }
    }

    internal static bool HasCurrentPackageIdentity()
    {
        try
        {
            int length = 0;
            int result = GetCurrentPackageFullName(ref length, null);
            return result == ERROR_SUCCESS || result == ERROR_INSUFFICIENT_BUFFER;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
    }

    [DllImport("shell32.dll")]
    private static extern int SHGetPropertyStoreForWindow(
        IntPtr hwnd,
        ref Guid iid,
        [MarshalAs(UnmanagedType.Interface)] out IPropertyStore? propertyStore);

    [DllImport("ole32.dll")]
    private static extern int PropVariantClear(ref PROPVARIANT propvar);

    private static readonly PROPERTYKEY PKEY_AppUserModel_ID = new(
        new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"),
        5);

    internal static string GetWindowAppUserModelId(IntPtr hWnd)
    {
        Guid iid = typeof(IPropertyStore).GUID;
        int hr = SHGetPropertyStoreForWindow(hWnd, ref iid, out IPropertyStore? propertyStore);
        if (hr != 0 || propertyStore == null)
            return string.Empty;

        try
        {
            PROPERTYKEY key = PKEY_AppUserModel_ID;
            int valueHr = propertyStore.GetValue(ref key, out PROPVARIANT propVariant);
            if (valueHr != 0)
                return string.Empty;

            using (propVariant)
            {
                return propVariant.GetString() ?? string.Empty;
            }
        }
        catch
        {
            return string.Empty;
        }
        finally
        {
            Marshal.ReleaseComObject(propertyStore);
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

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

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

    internal static string GetWindowClassName(IntPtr hWnd)
    {
        var buf = new char[256];
        int len = GetClassName(hWnd, buf, buf.Length);
        return len <= 0 ? string.Empty : new string(buf, 0, len);
    }

    internal static bool IsAltTabWindow(IntPtr hWnd)
    {
        WindowCandidateData window = new(
            IsVisible: IsWindowVisible(hWnd),
            ExStyle: (nint)GetWindowLongPtr(hWnd, GWL_EXSTYLE),
            HasOwner: GetWindow(hWnd, GW_OWNER) != IntPtr.Zero,
            ClassName: GetWindowClassName(hWnd),
            Title: GetWindowTitle(hWnd));
        return WindowEnumerationPolicy.ShouldInclude(window);
    }

    internal static bool IsLikelyOwnedStandardDialog(IntPtr hWnd)
    {
        WindowCandidateData window = new(
            IsVisible: IsWindowVisible(hWnd),
            ExStyle: (nint)GetWindowLongPtr(hWnd, GWL_EXSTYLE),
            HasOwner: GetWindow(hWnd, GW_OWNER) != IntPtr.Zero,
            ClassName: GetWindowClassName(hWnd),
            Title: GetWindowTitle(hWnd));
        return WindowEnumerationPolicy.IsLikelyOwnedStandardDialog(window);
    }

    internal static double GetWindowAspectRatio(IntPtr hWnd, double fallback = 16.0 / 9.0)
    {
        if (hWnd == IntPtr.Zero || !GetWindowRect(hWnd, out var rect))
            return fallback;

        int width = rect.Right - rect.Left;
        int height = rect.Bottom - rect.Top;
        if (width <= 0 || height <= 0)
            return fallback;

        return (double)width / height;
    }

    [ComImport]
    [Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        int GetCount(out uint propertyCount);
        int GetAt(uint propertyIndex, out PROPERTYKEY key);
        int GetValue(ref PROPERTYKEY key, out PROPVARIANT value);
        int SetValue(ref PROPERTYKEY key, ref PROPVARIANT value);
        int Commit();
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct PROPERTYKEY(Guid formatId, uint propertyId)
    {
        public readonly Guid fmtid = formatId;
        public readonly uint pid = propertyId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROPVARIANT : IDisposable
    {
        private ushort vt;
        private readonly ushort reserved1;
        private readonly ushort reserved2;
        private readonly ushort reserved3;
        private IntPtr pointerValue;
        private readonly IntPtr extraValue;

        public string? GetString() => vt == 31 ? Marshal.PtrToStringUni(pointerValue) : null;

        public void Dispose() => PropVariantClear(ref this);
    }
}

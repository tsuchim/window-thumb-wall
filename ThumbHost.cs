using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace WindowThumbWall;

/// <summary>
/// Lightweight HwndHost that creates a child HWND used as the
/// positional anchor for a DWM thumbnail.
/// </summary>
internal sealed class ThumbHost : HwndHost
{
    private IntPtr _hwnd;
    internal IntPtr Hwnd => _hwnd;

    protected override HandleRef BuildWindowCore(HandleRef hwndParent)
    {
        _hwnd = NativeMethods.CreateWindowEx(
            0, "Static", "",
            NativeMethods.WS_CHILD | NativeMethods.WS_CLIPCHILDREN,
            0, 0, 1, 1,
            hwndParent.Handle, IntPtr.Zero,
            NativeMethods.GetModuleHandle(null), IntPtr.Zero);

        return new HandleRef(this, _hwnd);
    }

    protected override void DestroyWindowCore(HandleRef hwnd)
    {
        NativeMethods.DestroyWindow(hwnd.Handle);
    }
}

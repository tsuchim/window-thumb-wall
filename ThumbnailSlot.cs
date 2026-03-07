namespace WindowThumbWall;

/// <summary>
/// Manages one DWM thumbnail registration bound to a <see cref="ThumbHost"/> cell.
/// The thumbnail is rendered by the DWM compositor on the top-level owner window
/// at the screen position that corresponds to the child HWND of the ThumbHost.
/// </summary>
internal sealed class ThumbnailSlot
{
    private readonly ThumbHost _host;
    private readonly IntPtr _ownerHwnd;
    private IntPtr _thumbId;

    internal IntPtr SourceHwnd { get; private set; }
    internal string SourceTitle { get; private set; } = string.Empty;
    internal string SourceProcessName { get; private set; } = string.Empty;
    internal bool IsOccupied => SourceHwnd != IntPtr.Zero;

    internal ThumbnailSlot(ThumbHost host, IntPtr ownerHwnd)
    {
        _host = host;
        _ownerHwnd = ownerHwnd;
    }

    internal bool Assign(IntPtr sourceHwnd, string title, string processName)
    {
        Clear();
        if (_host.Hwnd == IntPtr.Zero) return false;

        int hr = NativeMethods.DwmRegisterThumbnail(_ownerHwnd, sourceHwnd, out var id);
        if (hr != 0) return false;

        _thumbId = id;
        SourceHwnd = sourceHwnd;
        SourceTitle = title;
        SourceProcessName = processName;
        UpdateThumbnail();
        return true;
    }

    internal void Clear()
    {
        if (_thumbId != IntPtr.Zero)
        {
            NativeMethods.DwmUnregisterThumbnail(_thumbId);
            _thumbId = IntPtr.Zero;
        }
        SourceHwnd = IntPtr.Zero;
        SourceTitle = string.Empty;
        SourceProcessName = string.Empty;
    }

    /// <summary>
    /// Recalculates the destination rectangle from the ThumbHost's native HWND
    /// position relative to the owner window's client area, then pushes it to DWM.
    /// Also auto-hides the thumbnail when the host HWND is not visible.
    /// </summary>
    internal void UpdateThumbnail()
    {
        if (_thumbId == IntPtr.Zero || _host.Hwnd == IntPtr.Zero) return;

        // If the host HWND is hidden (e.g. cell collapsed), hide the DWM thumbnail.
        if (!NativeMethods.IsWindowVisible(_host.Hwnd))
        {
            var hide = new NativeMethods.DWM_THUMBNAIL_PROPERTIES
            {
                dwFlags = NativeMethods.DWM_TNP_VISIBLE,
                fVisible = false
            };
            NativeMethods.DwmUpdateThumbnailProperties(_thumbId, ref hide);
            return;
        }

        NativeMethods.GetClientRect(_host.Hwnd, out var hostRect);
        if (hostRect.Right <= 0 || hostRect.Bottom <= 0) return;

        var hostPt = new NativeMethods.POINT();
        NativeMethods.ClientToScreen(_host.Hwnd, ref hostPt);

        var mainPt = new NativeMethods.POINT();
        NativeMethods.ClientToScreen(_ownerHwnd, ref mainPt);

        int left   = hostPt.X - mainPt.X;
        int top    = hostPt.Y - mainPt.Y;
        int right  = left + hostRect.Right;
        int bottom = top + hostRect.Bottom;

        var props = new NativeMethods.DWM_THUMBNAIL_PROPERTIES
        {
            dwFlags = NativeMethods.DWM_TNP_RECTDESTINATION | NativeMethods.DWM_TNP_VISIBLE,
            rcDestination = new NativeMethods.RECT
            {
                Left = left,
                Top = top,
                Right = right,
                Bottom = bottom
            },
            fVisible = true
        };
        NativeMethods.DwmUpdateThumbnailProperties(_thumbId, ref props);
    }

    /// <summary>
    /// Returns false (and auto-clears) if the source window no longer exists.
    /// </summary>
    internal bool CheckValid()
    {
        if (SourceHwnd == IntPtr.Zero) return true;
        if (!NativeMethods.IsWindow(SourceHwnd))
        {
            Clear();
            return false;
        }
        return true;
    }
}

using System.Runtime.InteropServices;

namespace WindowThumbWall.Tests;

public sealed class NativeMethodsPropertyStoreTests
{
    [Fact]
    public void GetWindowAppUserModelId_ReadsKnownValueAndReturnsEmptyForMissingOrInvalidWindow()
    {
        if (!OperatingSystem.IsWindows())
            return;

        IntPtr assignedWindow = CreateHiddenWindow();
        IntPtr missingWindow = CreateHiddenWindow();

        try
        {
            const string expectedAppId = "WindowThumbWall.Tests.NativePropertyStore";
            SetWindowAppUserModelId(assignedWindow, expectedAppId);

            Assert.Equal(expectedAppId, NativeMethods.GetWindowAppUserModelId(assignedWindow));
            Assert.Equal(string.Empty, NativeMethods.GetWindowAppUserModelId(missingWindow));
            Assert.Equal(string.Empty, NativeMethods.GetWindowAppUserModelId(new IntPtr(-1)));
        }
        finally
        {
            DestroyWindow(assignedWindow);
            DestroyWindow(missingWindow);
        }
    }

    private static void SetWindowAppUserModelId(IntPtr hWnd, string appId)
    {
        Guid iid = typeof(IPropertyStore).GUID;
        int hr = SHGetPropertyStoreForWindow(hWnd, ref iid, out IPropertyStore? propertyStore);
        Marshal.ThrowExceptionForHR(hr);
        Assert.NotNull(propertyStore);

        try
        {
            PROPERTYKEY key = new(
                new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"),
                5);
            PROPVARIANT value = new(appId);
            try
            {
                Marshal.ThrowExceptionForHR(propertyStore.SetValue(ref key, ref value));
                Marshal.ThrowExceptionForHR(propertyStore.Commit());
            }
            finally
            {
                PropVariantClear(ref value);
            }
        }
        finally
        {
            Marshal.ReleaseComObject(propertyStore);
        }
    }

    private static IntPtr CreateHiddenWindow()
    {
        IntPtr hWnd = CreateWindowEx(
            0,
            "STATIC",
            "WindowThumbWall native interop test",
            0,
            0,
            0,
            0,
            0,
            IntPtr.Zero,
            IntPtr.Zero,
            IntPtr.Zero,
            IntPtr.Zero);
        Assert.NotEqual(IntPtr.Zero, hWnd);
        Assert.False(IsWindowVisible(hWnd));
        return hWnd;
    }

    [DllImport("shell32.dll")]
    private static extern int SHGetPropertyStoreForWindow(
        IntPtr hWnd,
        ref Guid iid,
        [MarshalAs(UnmanagedType.Interface)] out IPropertyStore? propertyStore);

    [DllImport("ole32.dll")]
    private static extern int PropVariantClear(ref PROPVARIANT value);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWindowEx(
        uint extendedStyle,
        string className,
        string windowName,
        uint style,
        int x,
        int y,
        int width,
        int height,
        IntPtr parent,
        IntPtr menu,
        IntPtr instance,
        IntPtr parameter);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [ComImport]
    [Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        [PreserveSig]
        int GetCount(out uint propertyCount);

        [PreserveSig]
        int GetAt(uint propertyIndex, out PROPERTYKEY key);

        [PreserveSig]
        int GetValue(ref PROPERTYKEY key, out PROPVARIANT value);

        [PreserveSig]
        int SetValue(ref PROPERTYKEY key, ref PROPVARIANT value);

        [PreserveSig]
        int Commit();
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct PROPERTYKEY(Guid formatId, uint propertyId)
    {
        public readonly Guid fmtid = formatId;
        public readonly uint pid = propertyId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROPVARIANT(string value)
    {
        private ushort vt = 31;
        private readonly ushort reserved1;
        private readonly ushort reserved2;
        private readonly ushort reserved3;
        private IntPtr pointerValue = Marshal.StringToCoTaskMemUni(value);
        private readonly IntPtr extraValue;
    }
}

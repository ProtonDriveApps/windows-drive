using System.Runtime.InteropServices;

namespace ProtonDrive.Sync.Windows.Interop;

internal static class Gdi32
{
    [DllImport(Libraries.Gdi32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DeleteObject(IntPtr hObject);
}

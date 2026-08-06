using System.Runtime.InteropServices;

namespace Proton.Drive.App.Windows.Interop;

internal static class User32
{
    [DllImport(Libraries.User32, SetLastError = true)]
    public static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport(Libraries.User32, SetLastError = true)]
    public static extern bool SetForegroundWindow(IntPtr hWnd);
}

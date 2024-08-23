using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace ProtonDrive.Sync.Windows.Interop;

public static partial class Kernel32
{
    [DllImport(Libraries.Kernel32, EntryPoint = "CreateDirectoryW", ExactSpelling = true, SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern unsafe bool CreateDirectory(
        string lpPathName,
        SECURITY_ATTRIBUTES* lpSecurityAttributes);
}

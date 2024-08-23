using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace ProtonDrive.Sync.Windows.Interop;

public static partial class Kernel32
{
    [DllImport(Libraries.Kernel32, ExactSpelling = true, SetLastError = true)]
    public static extern SafeFileHandle ReOpenFile(
        SafeFileHandle hOriginalFile,
        DesiredAccess dwDesiredAccess,
        FileShare dwShareMode,
        uint dwFlagsAndAttributes);
}

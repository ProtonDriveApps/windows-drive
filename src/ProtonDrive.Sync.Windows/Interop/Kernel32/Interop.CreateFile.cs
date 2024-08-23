using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace ProtonDrive.Sync.Windows.Interop;

public static partial class Kernel32
{
    [DllImport(Libraries.Kernel32, EntryPoint = "CreateFileW", ExactSpelling = true, SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern unsafe SafeFileHandle CreateFile(
        string lpFileName,
        DesiredAccess dwDesiredAccess,
        FileShare dwShareMode,
        SECURITY_ATTRIBUTES* lpSecurityAttributes,
        FileMode dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [DllImport(Libraries.Kernel32, EntryPoint = "CreateFileW", ExactSpelling = true, SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern unsafe SafeFileHandle CreateFile(
        string lpFileName,
        DesiredAccess dwDesiredAccess,
        FileShare dwShareMode,
        SECURITY_ATTRIBUTES* lpSecurityAttributes,
        FileMode dwCreationDisposition,
        uint dwFlagsAndAttributes,
        SafeFileHandle hTemplateFile);
}

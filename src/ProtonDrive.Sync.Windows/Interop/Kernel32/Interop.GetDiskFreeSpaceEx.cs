using System.Runtime.InteropServices;

namespace ProtonDrive.Sync.Windows.Interop;

public static partial class Kernel32
{
    [DllImport(Libraries.Kernel32, CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetDiskFreeSpaceEx(
        string lpDirectoryName,
        out ulong lpFreeBytesAvailable,
        out ulong lpTotalNumberOfBytes,
        out ulong lpTotalNumberOfFreeBytes);
}

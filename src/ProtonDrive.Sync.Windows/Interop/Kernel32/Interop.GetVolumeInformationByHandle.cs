using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace ProtonDrive.Sync.Windows.Interop;

public static partial class Kernel32
{
    /// <summary>
    /// Retrieves information about the file system and volume associated with the specified file.
    /// </summary>
    /// <returns>
    /// If all the requested information is retrieved, the return value is True.
    /// If not all the requested information is retrieved, the return value is False.
    /// To get extended error information, call GetLastError.
    /// </returns>
    [DllImport(Libraries.Kernel32, EntryPoint = "GetVolumeInformationByHandleW", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetVolumeInformationByHandle(
        SafeFileHandle hFile,
        StringBuilder volumeNameBuffer,
        int volumeNameSize,
        out uint volumeSerialNumber,
        out uint maximumComponentLength,
        out FileSystemFlags fileSystemFlags,
        StringBuilder fileSystemNameBuffer,
        int nFileSystemNameSize);
}

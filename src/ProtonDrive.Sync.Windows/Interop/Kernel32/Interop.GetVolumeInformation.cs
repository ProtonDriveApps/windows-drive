using System.Runtime.InteropServices;
using System.Text;

namespace ProtonDrive.Sync.Windows.Interop;

public static partial class Kernel32
{
    /// <summary>Retrieves information about the file system and volume associated with the specified root directory.</summary>
    /// <returns>
    /// If all the requested information is retrieved, the return value is nonzero.
    /// If not all the requested information is retrieved, the return value is zero.
    /// </returns>
    /// <remarks>Minimum supported client: Windows XP</remarks>
    /// <remarks>Minimum supported server: Windows Server 2003</remarks>
    /// <remarks>"lpRootPathName" must end with a trailing backslash.</remarks>
    [DllImport(Libraries.Kernel32, EntryPoint = "GetVolumeInformationW", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetVolumeInformation(
        string rootPathName,
        StringBuilder volumeNameBuffer,
        int volumeNameSize,
        out uint volumeSerialNumber,
        out uint maximumComponentLength,
        out FileSystemFlags fileSystemFlags,
        StringBuilder fileSystemNameBuffer,
        int nFileSystemNameSize);
}

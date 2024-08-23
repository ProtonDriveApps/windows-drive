using System.Runtime.InteropServices;
using System.Text;

namespace ProtonDrive.Sync.Windows.Interop;

public static partial class Kernel32
{
    /// <summary>Retrieves the volume mount point where the specified path is mounted.</summary>
    /// <returns>
    /// If the function succeeds, the return value is nonzero.
    /// If the function fails, the return value is zero. To get extended error information, call GetLastError.
    /// To get extended error information call Win32Exception().
    /// </returns>
    /// <remarks>Minimum supported client: Windows XP</remarks>
    /// <remarks>Minimum supported server: Windows Server 2003</remarks>
    [DllImport(Libraries.Kernel32, EntryPoint = "GetVolumePathNameW", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetVolumePathName(
        [MarshalAs(UnmanagedType.LPWStr)] string filePath,
        StringBuilder volumeNameBuffer,
        [MarshalAs(UnmanagedType.U4)] uint volumeNameSize);
}

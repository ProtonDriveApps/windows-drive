using System.Runtime.InteropServices;

namespace Proton.Drive.Sdk.Sync.Windows.Interop;

public static partial class Kernel32
{
    /// <summary>
    /// Determines whether a disk drive is a removable, fixed, CD-ROM, RAM disk, or network drive.
    /// </summary>
    /// <param name="lpRootPathName">
    /// The root directory for the drive.
    /// <para>
    /// A trailing backslash is required. If this parameter is <see langword="null"/>, the function uses the root of the current directory.
    /// </para>
    /// </param>
    /// <returns>The return value specifies the type of drive, see <see cref="DriveType"/>.</returns>
    [LibraryImport(Libraries.Kernel32, EntryPoint = "GetDriveTypeW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    public static partial DriveType GetDriveType([MarshalAs(UnmanagedType.LPWStr)] string? lpRootPathName);
}

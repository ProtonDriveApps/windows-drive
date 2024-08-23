using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace ProtonDrive.Sync.Windows.Interop;

public static partial class Kernel32
{
    [DllImport(Libraries.Kernel32, ExactSpelling = true, SetLastError = true)]
    public static extern bool GetFileInformationByHandle(
        SafeFileHandle hFile,
        ref BY_HANDLE_FILE_INFORMATION lpFileInformation);
}

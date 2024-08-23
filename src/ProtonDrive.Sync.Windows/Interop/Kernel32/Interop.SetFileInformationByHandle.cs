using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace ProtonDrive.Sync.Windows.Interop;

public static partial class Kernel32
{
    [DllImport(Libraries.Kernel32, ExactSpelling = true, SetLastError = true)]
    public static extern unsafe bool SetFileInformationByHandle(
        SafeFileHandle hFile,
        FILE_INFO_BY_HANDLE_CLASS FileInformationClass,
        void* lpFileInformation,
        uint dwBufferSize);
}

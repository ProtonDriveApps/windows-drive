using Microsoft.Win32.SafeHandles;
using ProtonDrive.Sync.Windows.Interop;

namespace ProtonDrive.Sync.Windows.FileSystem;

public static class SafeFileHandleExtensions
{
    public static bool CancelIo(this SafeFileHandle fileHandle)
    {
        return Kernel32.CancelIoEx(fileHandle);
    }
}

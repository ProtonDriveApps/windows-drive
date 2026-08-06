using Microsoft.Win32.SafeHandles;
using Proton.Drive.Sdk.Sync.Windows.Interop;

namespace Proton.Drive.Sdk.Sync.Windows.FileSystem;

public static class SafeFileHandleExtensions
{
    public static bool CancelIo(this SafeFileHandle fileHandle)
    {
        return Kernel32.CancelIoEx(fileHandle);
    }
}

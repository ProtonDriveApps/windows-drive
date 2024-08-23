using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace ProtonDrive.Sync.Windows.Interop;

public partial class Kernel32
{
    // Supported starting from Windows 10, version 1709 (desktop apps only)
    [DllImport(Libraries.Kernel32, EntryPoint = "ReadDirectoryChangesExW", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern unsafe bool ReadDirectoryChangesExW(
        SafeFileHandle hDirectory,
        byte[] lpBuffer,
        uint nBufferLength,
        bool bWatchSubtree,
        uint dwNotifyFilter,
        uint* lpBytesReturned,
        NativeOverlapped* lpOverlapped,
        void* lpCompletionRoutine,
        READ_DIRECTORY_NOTIFY_INFORMATION_CLASS ReadDirectoryNotifyInformationClass);
}

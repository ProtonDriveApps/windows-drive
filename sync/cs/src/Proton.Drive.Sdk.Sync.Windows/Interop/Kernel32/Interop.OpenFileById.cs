using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Proton.Drive.Sdk.Sync.Windows.Interop;

public static partial class Kernel32
{
    [DllImport(Libraries.Kernel32, ExactSpelling = true, SetLastError = true)]
    public static extern unsafe SafeFileHandle OpenFileById(
        SafeFileHandle hVolumeHint,
        ref Vanara.PInvoke.Kernel32.FILE_ID_DESCRIPTOR lpFileId,
        DesiredAccess dwDesiredAccess,
        FileShare dwShareMode,
        SECURITY_ATTRIBUTES* lpSecurityAttributes,
        uint dwFlagsAndAttributes);
}

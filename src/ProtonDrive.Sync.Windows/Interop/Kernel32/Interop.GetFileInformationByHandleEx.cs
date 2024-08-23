using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace ProtonDrive.Sync.Windows.Interop;

[SuppressMessage("StyleCop.CSharp.NamingRules", "SA1313:Parameter names should begin with lower-case letter", Justification = "Win32 naming convention")]
public static partial class Kernel32
{
    [DllImport(Libraries.Kernel32, ExactSpelling = true, SetLastError = true)]
    public static extern unsafe bool GetFileInformationByHandleEx(
        SafeFileHandle hFile,
        FILE_INFO_BY_HANDLE_CLASS FileInformationClass,
        void* lpFileInformation,
        uint dwBufferSize);
}

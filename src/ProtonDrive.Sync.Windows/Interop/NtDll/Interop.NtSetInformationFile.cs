using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace ProtonDrive.Sync.Windows.Interop;

[SuppressMessage("StyleCop.CSharp.NamingRules", "SA1313:Parameter names should begin with lower-case letter", Justification = "Win32 naming convention")]
public static partial class NtDll
{
    [DllImport(Libraries.NtDll, ExactSpelling = true)]
    public static extern unsafe NTSTATUS NtSetInformationFile(
        SafeFileHandle FileHandle,
        out IO_STATUS_BLOCK IoStatusBlock,
        void* FileInformation,
        int Length,
        FILE_INFORMATION_CLASS FileInformationClass);
}

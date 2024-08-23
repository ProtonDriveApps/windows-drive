using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace ProtonDrive.Sync.Windows.Interop;

[SuppressMessage("StyleCop.CSharp.NamingRules", "SA1313:Parameter names should begin with lower-case letter", Justification = "Win32 naming convention")]
public static partial class NtDll
{
    // https://docs.microsoft.com/en-us/windows-hardware/drivers/ddi/ntifs/nf-ntifs-ntquerydirectoryfile
    [DllImport(Libraries.NtDll, CharSet = CharSet.Unicode, ExactSpelling = true)]
    public static extern unsafe NTSTATUS NtQueryDirectoryFile(
        SafeFileHandle FileHandle,
        IntPtr Event,
        IntPtr ApcRoutine,
        IntPtr ApcContext,
        out IO_STATUS_BLOCK IoStatusBlock,
        void* FileInformation,
        int Length,
        FILE_INFORMATION_CLASS FileInformationClass,
        [MarshalAs(UnmanagedType.U1)]
        bool ReturnSingleEntry,
        UNICODE_STRING? FileName,
        [MarshalAs(UnmanagedType.U1)]
        bool RestartScan);
}

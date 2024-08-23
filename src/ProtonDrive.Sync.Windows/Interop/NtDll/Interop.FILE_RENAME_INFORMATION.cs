using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace ProtonDrive.Sync.Windows.Interop;

[SuppressMessage("StyleCop.CSharp.NamingRules", "SA1310:Field names should not contain underscore", Justification = "Win32 naming convention")]
public static partial class NtDll
{
    /// <remarks>
    /// See https://docs.microsoft.com/en-us/windows/win32/api/winbase/ns-winbase-file_rename_info
    /// and https://docs.microsoft.com/en-us/windows-hardware/drivers/ddi/ntifs/ns-ntifs-_file_rename_information
    /// for more information on renaming files.
    /// </remarks>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct FILE_RENAME_INFORMATION
    {
        [MarshalAs(UnmanagedType.U1)]
        public bool ReplaceIfExists;
        public IntPtr RootDirectory;
        public int FileNameLength;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string FileName;
    }
}

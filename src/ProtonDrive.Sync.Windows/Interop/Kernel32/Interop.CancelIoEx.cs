using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace ProtonDrive.Sync.Windows.Interop;

public static partial class Kernel32
{
    /// <summary>
    /// Marks any outstanding I/O operations for the specified file handle.
    /// The function only cancels I/O operations in the current process,
    /// regardless of which thread created the I/O operation.
    /// </summary>
    /// <param name="hFile">A handle to the file.</param>
    /// <param name="lpOverlapped">A pointer to an <see cref="NativeOverlapped"/> data structure
    /// that contains the data used for asynchronous I/O.</param>
    /// <returns>
    /// If the function succeeds, the return value is nonzero. The cancel operation for all pending I/O operations
    /// issued by the calling process for the specified file handle was successfully requested.
    /// </returns>
    [DllImport(Libraries.Kernel32, EntryPoint = "CancelIoEx", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool CancelIoEx([In] SafeFileHandle hFile, [In] IntPtr lpOverlapped = default);

    /// <summary>
    /// Marks any outstanding I/O operations for the specified file handle.
    /// The function only cancels I/O operations in the current process,
    /// regardless of which thread created the I/O operation.
    /// </summary>
    /// <param name="hFile">A handle to the file.</param>
    /// <param name="lpOverlapped">A pointer to an <see cref="NativeOverlapped"/> data structure
    /// that contains the data used for asynchronous I/O.</param>
    /// <returns>
    /// If the function succeeds, the return value is nonzero. The cancel operation for all pending I/O operations
    /// issued by the calling process for the specified file handle was successfully requested.
    /// </returns>
    [DllImport(Libraries.Kernel32, EntryPoint = "CancelIoEx", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern unsafe bool CancelIoEx([In] SafeFileHandle hFile, [In] NativeOverlapped* lpOverlapped);
}

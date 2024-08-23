// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See LICENSE-MIT file in the project root for full license information.

using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using ProtonDrive.Shared.IO;
using ProtonDrive.Sync.Windows.Interop;
using CldApi = Vanara.PInvoke.CldApi;

namespace ProtonDrive.Sync.Windows.FileSystem.Internal;

internal static class FileSystem
{
    public static unsafe SafeFileHandle CreateHandle(
        string path,
        FileMode mode,
        FileSystemFileAccess access,
        FileShare share,
        FileAttributes attributes,
        FileOptions options,
        SafeFileHandle? templateFileHandle = null)
    {
        Kernel32.SECURITY_ATTRIBUTES secAttrs = GetSecAttrs(share);

        // Our Inheritable bit was stolen from Windows, but should be set in
        // the security attributes class. Don't leave this bit set.
        share &= ~FileShare.Inheritable;

        // Must use a valid Win32 constant here...
        if (mode == FileMode.Append)
        {
            mode = FileMode.OpenOrCreate;
        }

        var flagsAndAttributes = (uint)options | (uint)attributes;

        // For mitigating local elevation of privilege attack through named pipes
        // make sure we always call CreateFile with SECURITY_ANONYMOUS so that the
        // named pipe server can't impersonate a high privileged client security context
        // (note that this is the effective default on CreateFile2)
        flagsAndAttributes |= Kernel32.SecurityOptions.SECURITY_SQOS_PRESENT | Kernel32.SecurityOptions.SECURITY_ANONYMOUS;

        using (DisableMediaInsertionPrompt.Create())
        {
            path = PathInternal.EnsureExtendedPrefixIfNeeded(path);

            return ValidFileHandle(
                templateFileHandle == null
                    ? Kernel32.CreateFile(path, (Kernel32.DesiredAccess)access, share, &secAttrs, mode, flagsAndAttributes, IntPtr.Zero)
                    : Kernel32.CreateFile(path, (Kernel32.DesiredAccess)access, share, &secAttrs, mode, flagsAndAttributes, templateFileHandle),
                path);
        }
    }

    public static SafeFileHandle ReOpenFile(
        SafeFileHandle handle,
        FileSystemFileAccess access,
        FileShare share,
        FileAttributes attributes,
        FileOptions options)
    {
        // Reopening the directory handle returns Access denied error (error number 5). Not clear why.

        // Inheritable bit is not needed here.
        share &= ~FileShare.Inheritable;
        var flagsAndAttributes = (uint)options | (uint)attributes;

        return ValidFileHandle(Kernel32.ReOpenFile(handle, (Kernel32.DesiredAccess)access, share, flagsAndAttributes));
    }

    public static Kernel32.BY_HANDLE_FILE_INFORMATION GetFileInformation(SafeFileHandle handle)
    {
        Kernel32.BY_HANDLE_FILE_INFORMATION data = default;

        if (!Kernel32.GetFileInformationByHandle(handle, ref data))
        {
            throw Win32Marshal.GetExceptionForLastWin32Error();
        }

        return data;
    }

    public static unsafe void SetFileInformation(SafeFileHandle handle, Kernel32.FILE_BASIC_INFO data)
    {
        if (!Kernel32.SetFileInformationByHandle(
                handle,
                Kernel32.FILE_INFO_BY_HANDLE_CLASS.FileBasicInfo,
                &data,
                (uint)Marshal.SizeOf(data)))
        {
            throw Win32Marshal.GetExceptionForLastWin32Error();
        }
    }

    public static unsafe Kernel32.FILE_ATTRIBUTE_TAG_INFO GetFileAttributeTagInfo(SafeFileHandle handle)
    {
        Kernel32.FILE_ATTRIBUTE_TAG_INFO data = default;

        if (!Kernel32.GetFileInformationByHandleEx(
                handle,
                Kernel32.FILE_INFO_BY_HANDLE_CLASS.FileAttributeTagInfo,
                &data,
                (uint)Marshal.SizeOf(data)))
        {
            throw Win32Marshal.GetExceptionForLastWin32Error();
        }

        return data;
    }

    public static PlaceholderState GetPlaceholderState(FileAttributes attributes, uint reparseTag)
    {
        return (PlaceholderState)CldApi.CfGetPlaceholderStateFromAttributeTag((Vanara.PInvoke.FileFlagsAndAttributes)attributes, reparseTag);
    }

    /// <summary>
    /// Sets file size to zero.
    /// </summary>
    /// <param name="handle">File handle.</param>
    public static unsafe void TruncateFile(SafeFileHandle handle)
    {
        Kernel32.FILE_END_OF_FILE_INFO data = default;

        if (!Kernel32.SetFileInformationByHandle(
                handle,
                Kernel32.FILE_INFO_BY_HANDLE_CLASS.FileEndOfFileInfo,
                &data,
                (uint)Marshal.SizeOf(data)))
        {
            throw Win32Marshal.GetExceptionForLastWin32Error();
        }
    }

    /// <summary>
    /// Renames the file.
    /// </summary>
    /// <param name="handle">File handle.</param>
    /// <param name="name">The new name.</param>
    /// <param name="replaceIfExists">Specifies that if a file with the given name already exists, it should be replaced with the given file.</param>
    public static unsafe void Rename(SafeFileHandle handle, string name, bool replaceIfExists)
    {
        var data = new NtDll.FILE_RENAME_INFORMATION
        {
            ReplaceIfExists = replaceIfExists,
            RootDirectory = IntPtr.Zero,
            FileNameLength = name.Length * sizeof(char),
            FileName = name,
        };

        int dataLength = Marshal.SizeOf(data);
        IntPtr ptr = Marshal.AllocHGlobal(dataLength);
        try
        {
            Marshal.StructureToPtr(data, ptr, false);

            var status = NtDll.NtSetInformationFile(
                handle,
                out NtDll.IO_STATUS_BLOCK _,
                ptr.ToPointer(),
                dataLength,
                NtDll.FILE_INFORMATION_CLASS.FileRenameInformation);

            switch (status)
            {
                case NtDll.NTSTATUS.STATUS_SUCCESS:
                    return;

                default:
                    var error = (int)NtDll.RtlNtStatusToDosError(status);
                    throw Win32Marshal.GetExceptionForWin32Error(error);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }

    /// <summary>
    /// Moves the file or directory to the destination directory and optionally renames it.
    /// </summary>
    /// <param name="handle">File handle.</param>
    /// <param name="directoryHandle">The destination directory handle.</param>
    /// <param name="name">The new name under the destination directory.</param>
    /// <param name="replaceIfExists">Specifies that if a file with the given name already exists, it should be replaced with the given file.</param>
    public static unsafe void Move(SafeFileHandle handle, SafeFileHandle directoryHandle, string name, bool replaceIfExists)
    {
        var data = new NtDll.FILE_RENAME_INFORMATION
        {
            ReplaceIfExists = replaceIfExists,
            RootDirectory = directoryHandle.DangerousGetHandle(),
            FileNameLength = name.Length * sizeof(char),
            FileName = name,
        };

        int dataLength = Marshal.SizeOf(data);
        IntPtr ptr = Marshal.AllocHGlobal(dataLength);
        try
        {
            Marshal.StructureToPtr(data, ptr, false);

            var status = NtDll.NtSetInformationFile(
                handle,
                out NtDll.IO_STATUS_BLOCK _,
                ptr.ToPointer(),
                dataLength,
                NtDll.FILE_INFORMATION_CLASS.FileRenameInformation);

            switch (status)
            {
                case NtDll.NTSTATUS.STATUS_SUCCESS:
                    return;

                default:
                    var error = (int)NtDll.RtlNtStatusToDosError(status);
                    throw Win32Marshal.GetExceptionForWin32Error(error);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }

    /// <summary>
    /// Marks the file or directory for deletion. The object gets deleted after all handles to
    /// the object are closed.
    /// </summary>
    /// <param name="handle">File handle.</param>
    /// <param name="delete">Indicates whether to mark the file or directory for deletion
    /// or to remove marking.</param>
    /// <remarks>
    /// The handle need DELETE access requested in the call to the CreateFile function.
    /// </remarks>
    public static unsafe void Delete(SafeFileHandle handle, bool delete)
    {
        var data = new Kernel32.FILE_DISPOSITION_INFO
        {
            DeleteFile = delete ? BOOLEAN.TRUE : BOOLEAN.FALSE,
        };

        if (!Kernel32.SetFileInformationByHandle(
                handle,
                Kernel32.FILE_INFO_BY_HANDLE_CLASS.FileDispositionInfo,
                &data,
                (uint)Marshal.SizeOf(data)))
        {
            throw Win32Marshal.GetExceptionForLastWin32Error();
        }
    }

    private static unsafe Kernel32.SECURITY_ATTRIBUTES GetSecAttrs(FileShare share)
    {
        var secAttrs = new Kernel32.SECURITY_ATTRIBUTES
        {
            nLength = (uint)sizeof(Kernel32.SECURITY_ATTRIBUTES),
            InheritHandle = (share & FileShare.Inheritable) != 0,
        };

        return secAttrs;
    }

    private static SafeFileHandle ValidFileHandle(SafeFileHandle fileHandle, string path = "")
    {
        if (fileHandle.IsInvalid)
        {
            // Throw a meaningful exception with the full path.

            // NT5 oddity - when trying to open "C:\" as a Win32FileStream,
            // we usually get ERROR_PATH_NOT_FOUND from the OS.  We should
            // probably be consistent w/ every other directory.
            int errorCode = Marshal.GetLastWin32Error();

            if (errorCode == Errors.ERROR_PATH_NOT_FOUND && path.Length == PathInternal.GetRootLength(path))
            {
                errorCode = Errors.ERROR_ACCESS_DENIED;
            }

            throw Win32Marshal.GetExceptionForWin32Error(errorCode, path);
        }

        return fileHandle;
    }
}

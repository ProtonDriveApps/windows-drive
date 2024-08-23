// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See LICENSE-MIT file in the project root for full license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace ProtonDrive.Sync.Windows.FileSystem.Internal;

internal static class Win32Marshal
{
    /// <summary>
    /// Converts the last Win32 error into a corresponding <see cref="Exception"/> object, optionally
    /// including the specified path in the error message.
    /// </summary>
    internal static Exception GetExceptionForLastWin32Error(string? path = "")
    {
        var errorCode = Marshal.GetLastWin32Error();

        throw GetExceptionForWin32Error(errorCode, path);
    }

    /// <summary>
    /// Converts the specified Win32 error into a corresponding <see cref="Exception"/> object, optionally
    /// including the specified path in the error message.
    /// </summary>
    internal static Exception GetExceptionForWin32Error(int errorCode, string? path = "")
    {
        // ERROR_SUCCESS gets thrown when another unexpected interop call was made before checking GetLastWin32Error().
        // Errors have to get retrieved as soon as possible after P/Invoking to avoid this.
        Debug.Assert(errorCode != Interop.Errors.ERROR_SUCCESS, "errorCode != Interop.Errors.ERROR_SUCCESS");

        // The file path is cleared to avoid sensitive information from appearing in the logs.
        path = null;

        switch (errorCode)
        {
            case Interop.Errors.ERROR_FILE_NOT_FOUND:
                return new FileNotFoundException(
                    string.IsNullOrEmpty(path)
                        ? "Could not find the specified file"
                        : $"Could not find file '{path}'",
                    path);

            case Interop.Errors.ERROR_PATH_NOT_FOUND:
                return new DirectoryNotFoundException(
                    string.IsNullOrEmpty(path)
                        ? "Could not find a part of the path"
                        : $"Could not find a part of the path '{path}'");

            case Interop.Errors.ERROR_ACCESS_DENIED:
                return new UnauthorizedAccessException(
                    string.IsNullOrEmpty(path)
                        ? "Access to the path is denied"
                        : $"Access to the path '{path}' is denied");

            case Interop.Errors.ERROR_ALREADY_EXISTS:
                if (string.IsNullOrEmpty(path))
                    goto default;
                return new IOException(
                    $"Cannot create '{path}' because a file or directory with the same name already exists",
                    MakeHRFromErrorCode(errorCode));

            case Interop.Errors.ERROR_FILENAME_EXCED_RANGE:
                return new PathTooLongException(
                    string.IsNullOrEmpty(path)
                        ? "The specified file name or path is too long, or a component of the specified path is too long"
                        : $"The path '{path}' is too long, or a component of the specified path is too long");

            case Interop.Errors.ERROR_SHARING_VIOLATION:
                return new IOException(
                    string.IsNullOrEmpty(path)
                        ? "The process cannot access the file because it is being used by another process"
                        : $"The process cannot access the file '{path}' because it is being used by another process",
                    MakeHRFromErrorCode(errorCode));

            case Interop.Errors.ERROR_FILE_EXISTS:
                if (string.IsNullOrEmpty(path))
                    goto default;
                return new IOException(
                    $"The file '{path}' already exists",
                    MakeHRFromErrorCode(errorCode));

            case Interop.Errors.ERROR_OPERATION_ABORTED:
                return new OperationCanceledException();

            default:
                return new IOException(
                    string.IsNullOrEmpty(path) ? GetMessage(errorCode) : $"{GetMessage(errorCode)} : '{path}'",
                    MakeHRFromErrorCode(errorCode));
        }
    }

    /// <summary>
    /// If not already an HRESULT, returns an HRESULT for the specified Win32 error code.
    /// </summary>
    internal static int MakeHRFromErrorCode(int errorCode)
    {
        // Don't convert it if it is already an HRESULT
        if ((0xFFFF0000 & errorCode) != 0)
            return errorCode;

        return unchecked(((int)0x80070000) | errorCode);
    }

    /// <summary>
    /// Returns a string message for the specified Win32 error code.
    /// </summary>
    internal static string GetMessage(int errorCode) => Marshal.GetExceptionForHR(MakeHRFromErrorCode(errorCode))?.Message
                                                        ?? $"Win32 error {errorCode} occurred";
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.InteropServices;
using ProtonDrive.Sync.Shared.FileSystem;
using Vanara.PInvoke;

namespace ProtonDrive.Sync.Windows.FileSystem.Client;

internal static class ExceptionMapping
{
    public static bool TryMapException(Exception exception, long id, bool includeObjectId, [MaybeNullWhen(false)] out Exception mappedException)
    {
        /* The original exception message might contain the file system object path,
           that we assume is a sensitive information of the user. To avoid logging it,
           we do not set inner exception. */

        mappedException = exception switch
        {
            FileNotFoundException
                => CreateFileSystemClientException(FileSystemErrorCode.PathNotFound, innerException: null),
            DirectoryNotFoundException
                => CreateFileSystemClientException(FileSystemErrorCode.DirectoryNotFound, innerException: null),
            TypeMismatchException
                => CreateFileSystemClientException(FileSystemErrorCode.MetadataMismatch, innerException: null),
            IOException ex
                => CreateFileSystemClientExceptionFromResultCode(ex),
            COMException ex
                => CreateFileSystemClientExceptionFromResultCode(ex),
            UnauthorizedAccessException
                => CreateFileSystemClientException(FileSystemErrorCode.UnauthorizedAccess, innerException: null),
            _ =>
                null,
        };

        return mappedException is not null;

        Exception CreateFileSystemClientExceptionFromResultCode(Exception ex)
        {
            var hr = (HRESULT)ex.HResult;

            return hr.Facility switch
            {
                HRESULT.FacilityCode.FACILITY_WIN32 => CreateFileSystemClientExceptionForWin32Error(ex),
                HRESULT.FacilityCode.FACILITY_SHELL => CreateFileSystemClientExceptionForShellError(ex),
                _ => CreateFileSystemClientException(FileSystemErrorCode.Unknown, innerException: ex),
            };
        }

        Exception CreateFileSystemClientExceptionForWin32Error(Exception ex)
        {
            var hr = (HRESULT)ex.HResult;

            return hr.Code switch
            {
                Interop.Errors.ERROR_ALREADY_EXISTS or Interop.Errors.ERROR_FILE_EXISTS
                    => CreateFileSystemClientException(FileSystemErrorCode.DuplicateName, innerException: default),
                Interop.Errors.ERROR_SHARING_VIOLATION
                    => CreateFileSystemClientException(FileSystemErrorCode.SharingViolation, innerException: default),
                _
                    => CreateFileSystemClientException(FileSystemErrorCode.Unknown, innerException: ex),
            };
        }

        Exception CreateFileSystemClientExceptionForShellError(Exception ex)
        {
            return ex.HResult switch
            {
                HRESULT.COPYENGINE_E_SHARING_VIOLATION_DEST or HRESULT.COPYENGINE_E_SHARING_VIOLATION_SRC
                    => CreateFileSystemClientException(FileSystemErrorCode.SharingViolation, innerException: default),
                _
                    => CreateFileSystemClientException(FileSystemErrorCode.Unknown, innerException: ex),
            };
        }

        Exception CreateFileSystemClientException(FileSystemErrorCode errorCode, Exception? innerException)
        {
            return new FileSystemClientException<long>(
                errorCode,
                objectId: includeObjectId ? id : default,
                innerException)
            {
                // IOException might contain the error message suitable for displaying in the UI
                IsInnerExceptionMessageAuthoritative = innerException is not null,
            };
        }
    }

    public static bool TryMapException(Exception exception, long? id, [MaybeNullWhen(false)] out Exception mappedException)
    {
        return TryMapException(exception, id ?? default, id != default, out mappedException);
    }

    [DebuggerHidden]
    [StackTraceHidden]
    public static void InvokeWithExceptionMapping(Action action, long? id)
    {
        try
        {
            action.Invoke();
        }
        catch (Exception ex) when (TryMapException(ex, id, out var mappedException))
        {
            throw mappedException;
        }
    }

    [DebuggerHidden]
    [StackTraceHidden]
    public static T InvokeWithExceptionMapping<T>(Func<T> function, long? id)
    {
        try
        {
            return function.Invoke();
        }
        catch (Exception ex) when (TryMapException(ex, id, out var mappedException))
        {
            throw mappedException;
        }
    }

    [DebuggerHidden]
    [StackTraceHidden]
    public static IEnumerable<T> WithExceptionMapping<T>(this IEnumerable<T> origin, long id)
    {
        using var enumerator = InvokeWithExceptionMapping(origin.GetEnumerator, id);

        while (true)
        {
            if (!InvokeWithExceptionMapping(enumerator.MoveNext, id))
            {
                yield break;
            }

            // ReSharper disable once AccessToDisposedClosure
            yield return enumerator.Current;
        }
    }
}

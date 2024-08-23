using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Security.Cryptography;
using ProtonDrive.Client.Cryptography;
using ProtonDrive.Client.FileUploading;
using ProtonDrive.Sync.Shared.FileSystem;

namespace ProtonDrive.Client;

internal static class ExceptionMapping
{
    public static bool TryMapException(Exception exception, string? id, bool includeObjectId, [MaybeNullWhen(false)] out Exception mappedException)
    {
        mappedException = exception switch
        {
            ApiException ex => CreateFileSystemClientException(ToErrorCode(ex.ResponseCode)),
            CryptographicException => CreateFileSystemClientException(FileSystemErrorCode.Unknown),
            KeyPassphraseUnavailableException => CreateFileSystemClientException(FileSystemErrorCode.Unknown),
            IOException => CreateFileSystemClientException(FileSystemErrorCode.Unknown),
            AggregateException => CreateFileSystemClientException(FileSystemErrorCode.Unknown),
            BlockVerificationFailedException => CreateFileSystemClientException(FileSystemErrorCode.IntegrityFailure),
            _ => null,
        };

        return mappedException is not null;

        FileSystemClientException<string> CreateFileSystemClientException(FileSystemErrorCode errorCode)
        {
            return new FileSystemClientException<string>(
                $"{errorCode}",
                errorCode,
                objectId: includeObjectId ? id : default,
                exception)
            {
                // ApiException might contain the error message suitable for displaying in the UI
                IsInnerExceptionMessageAuthoritative = exception is ApiException { IsMessageAuthoritative: true } or IOException,
            };
        }
    }

    private static FileSystemErrorCode ToErrorCode(ResponseCode value) => value switch
    {
        ResponseCode.AlreadyExists => FileSystemErrorCode.DuplicateName,
        ResponseCode.DoesNotExist => FileSystemErrorCode.ObjectNotFound,
        ResponseCode.InvalidEncryptedIdFormat => FileSystemErrorCode.ObjectNotFound,
        ResponseCode.InvalidRequirements => FileSystemErrorCode.Unknown,
        ResponseCode.TooManyChildren => FileSystemErrorCode.TooManyChildren,
        ResponseCode.Timeout => FileSystemErrorCode.TimedOut,
        ResponseCode.RequestTimeout => FileSystemErrorCode.TimedOut,
        ResponseCode.Offline => FileSystemErrorCode.Offline,
        ResponseCode.InvalidVerificationToken => FileSystemErrorCode.IntegrityFailure,
        _ => FileSystemErrorCode.Unknown,
    };
}

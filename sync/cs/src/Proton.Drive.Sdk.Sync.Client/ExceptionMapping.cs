using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;
using Polly.CircuitBreaker;
using Proton.Drive.Sdk;
using Proton.Drive.Sdk.Nodes;
using Proton.Drive.Sdk.Nodes.Download;
using Proton.Drive.Sdk.Nodes.Upload;
using Proton.Drive.Sdk.Nodes.Upload.Verification;
using Proton.Drive.Sdk.Sync.Client.Cryptography;
using Proton.Drive.Sdk.Sync.Shared.FileSystem;
using Proton.Drive.Shared.Client;
using Proton.Sdk;

namespace Proton.Drive.Sdk.Sync.Client;

internal static class ExceptionMapping
{
    public static bool TryMapSdkClientException(Exception exception, string? id, bool includeObjectId, [MaybeNullWhen(false)] out FileSystemClientException mappedException)
    {
        mappedException = null;

        if (TryMapException(exception, id, includeObjectId, out mappedException))
        {
            return true;
        }

        // Proton SDK and Proton Drive SDK lets some HTTP client exceptions to bubble up
        return TryMapHttpClientException(exception, out var apiException) && TryMapException(apiException, id, includeObjectId, out mappedException);
    }

    public static bool TryMapException(Exception exception, string? id, bool includeObjectId, [MaybeNullWhen(false)] out FileSystemClientException mappedException)
    {
        mappedException = exception switch
        {
            ApiException ex => CreateFileSystemClientException(ToErrorCode(ex.ResponseCode)),
            CryptographicException => CreateFileSystemClientException(FileSystemErrorCode.Unknown),
            KeyPassphraseUnavailableException => CreateFileSystemClientException(FileSystemErrorCode.Unknown),
            IOException => CreateFileSystemClientException(FileSystemErrorCode.Unknown),
            AggregateException => CreateFileSystemClientException(FileSystemErrorCode.Unknown),

            // Proton SDK exceptions
            TooManyRequestsException => CreateFileSystemClientException(FileSystemErrorCode.RateLimited),
            ProtonApiException ex => CreateFileSystemClientException(ToErrorCode(ex.Code)),

            // Proton Drive SDK exceptions
            NodeKeyAndSessionKeyMismatchException => CreateFileSystemClientException(FileSystemErrorCode.IntegrityFailure),
            SessionKeyAndDataPacketMismatchException => CreateFileSystemClientException(FileSystemErrorCode.IntegrityFailure),
            DataIntegrityException => CreateFileSystemClientException(FileSystemErrorCode.IntegrityFailure),
            IntegrityException => CreateFileSystemClientException(FileSystemErrorCode.IntegrityFailure),
            CompletedDownloadManifestVerificationException => CreateFileSystemClientException(FileSystemErrorCode.IntegrityFailure),
            NodeMetadataDecryptionException => CreateFileSystemClientException(FileSystemErrorCode.Unknown),
            FileContentsDecryptionException => CreateFileSystemClientException(FileSystemErrorCode.Unknown),
            NodeWithSameNameExistsException => CreateFileSystemClientException(FileSystemErrorCode.DuplicateName),
            NodeNotFoundException => CreateFileSystemClientException(FileSystemErrorCode.ObjectNotFound),
            RevisionDraftConflictException => CreateFileSystemClientException(FileSystemErrorCode.Unknown),
            InvalidNodeTypeException => CreateFileSystemClientException(FileSystemErrorCode.Unknown),
            ValidationException ex => CreateFileSystemClientException(ToErrorCode(ex.Code ?? Proton.Sdk.Api.ResponseCode.CustomCode)),
            ProtonDriveException => CreateFileSystemClientException(FileSystemErrorCode.Unknown),

            // Proton SDK and Proton Drive SDK lets some HTTP client exceptions to bubble up
            not null when TryMapHttpClientException(exception, out var apiException) => CreateFileSystemClientException(ToErrorCode(apiException.ResponseCode)),

            _ => null,
        };

        return mappedException is not null;

        FileSystemClientException<string> CreateFileSystemClientException(FileSystemErrorCode errorCode)
        {
            var isMessageAuthoritative = exception
                is ApiException { IsMessageAuthoritative: true }
                or ProtonApiException
                or ProtonDriveException { InnerException: ProtonApiException }
                or IOException;

            return new FileSystemClientException<string>(
                $"{errorCode}",
                errorCode,
                objectId: includeObjectId ? id : null,
                exception)
            {
                // ApiException might contain the error message suitable for displaying in the UI
                IsInnerExceptionMessageAuthoritative = isMessageAuthoritative,
            };
        }
    }

    public static bool TryMapHttpClientException(Exception exception, [MaybeNullWhen(false)] out ApiException mappedException)
    {
        mappedException = exception switch
        {
            HttpRequestException { HttpRequestError: HttpRequestError.NameResolutionError or HttpRequestError.ConnectionError or HttpRequestError.ProxyTunnelError } ex => new ApiException(httpStatusCode: default, ResponseCode.NetworkError, ex.Message, ex),
            HttpRequestException { HttpRequestError: HttpRequestError.InvalidResponse or HttpRequestError.ResponseEnded } ex => new ApiException(httpStatusCode: default, ResponseCode.ServerError, ex.Message, ex),
            HttpRequestException { StatusCode: HttpStatusCode.RequestTimeout } ex => new ApiException(ex.StatusCode.Value, ResponseCode.ServerError, ex.Message, ex),
            HttpRequestException { StatusCode: >= HttpStatusCode.InternalServerError and < (HttpStatusCode)600 } ex => new ApiException(ex.StatusCode.Value, ResponseCode.ServerError, ex.Message, ex),
            HttpRequestException { StatusCode: not null } ex => new ApiException(ex.StatusCode.Value, (ResponseCode)ex.StatusCode.Value, ex.Message, ex),
            HttpRequestException { InnerException: SocketException socketException } ex => new ApiException(ToResponseCode(socketException), socketException.Message, ex),
            HttpRequestException ex => new ApiException(ResponseCode.Unknown, ex.InnerException?.Message ?? ex.Message, ex),

            HttpIOException { HttpRequestError: HttpRequestError.NameResolutionError or HttpRequestError.ConnectionError or HttpRequestError.ProxyTunnelError } ex => new ApiException(httpStatusCode: default, ResponseCode.NetworkError, ex.Message, ex),
            HttpIOException { HttpRequestError: HttpRequestError.InvalidResponse or HttpRequestError.ResponseEnded } ex => new ApiException(httpStatusCode: default, ResponseCode.ServerError, ex.Message, ex),
            HttpIOException ex => new ApiException(ResponseCode.Unknown, "API request failed", ex),

            TimeoutException ex => new ApiException(ResponseCode.Timeout, "API request timed out", ex),
            TaskCanceledException { InnerException: TimeoutException } ex => new ApiException(ResponseCode.Timeout, "API request timed out", ex),
            JsonException ex => new ApiException(ResponseCode.Unknown, "Failed to deserialize JSON content", ex),
            BrokenCircuitException ex => new ApiException(ResponseCode.Offline, "API not available", ex),
            NotSupportedException ex => new ApiException(ResponseCode.Unknown, "API request failed", ex),

            _ => null,
        };

        return mappedException is not null;
    }

    public static bool IsSdkIntegrityFailure(Exception exception)
    {
        return exception is
            CryptographicException or
            NodeMetadataDecryptionException or
            FileContentsDecryptionException or
            NodeKeyAndSessionKeyMismatchException or
            SessionKeyAndDataPacketMismatchException or
            DataIntegrityException or
            IntegrityException;
    }

    private static FileSystemErrorCode ToErrorCode(Proton.Sdk.Api.ResponseCode value)
    {
        // Proton SDK API response codes match legacy API response codes
        return ToErrorCode((ResponseCode)value);
    }

    private static FileSystemErrorCode ToErrorCode(ResponseCode value) => value switch
    {
        ResponseCode.AlreadyExists => FileSystemErrorCode.DuplicateName,
        ResponseCode.DoesNotExist => FileSystemErrorCode.ObjectNotFound,
        ResponseCode.InvalidEncryptedIdFormat => FileSystemErrorCode.ObjectNotFound,
        ResponseCode.InsufficientQuota => FileSystemErrorCode.FreeSpaceExceeded,
        ResponseCode.InsufficientSpace => FileSystemErrorCode.FreeSpaceExceeded,
        ResponseCode.TooManyChildren => FileSystemErrorCode.TooManyChildren,
        ResponseCode.Timeout => FileSystemErrorCode.TimedOut,
        ResponseCode.Offline => FileSystemErrorCode.Offline,
        ResponseCode.InvalidVerificationToken => FileSystemErrorCode.IntegrityFailure,
        ResponseCode.TooManyRequests => FileSystemErrorCode.RateLimited,
        ResponseCode.NetworkError => FileSystemErrorCode.NetworkError,
        ResponseCode.ServerError => FileSystemErrorCode.ServerError,
        ResponseCode.MainPhotoAlreadyInAlbum => FileSystemErrorCode.MainPhotoAlreadyInAlbum,
        >= (ResponseCode)500 and < (ResponseCode)600 => FileSystemErrorCode.ServerError,
        _ => FileSystemErrorCode.Unknown,
    };

    private static ResponseCode ToResponseCode(SocketException exception)
    {
        return (exception.SocketErrorCode is SocketError.ConnectionRefused or
            SocketError.ConnectionAborted or
            SocketError.HostDown or
            SocketError.TimedOut)
            ? ResponseCode.ServerError
            : ResponseCode.NetworkError;
    }
}

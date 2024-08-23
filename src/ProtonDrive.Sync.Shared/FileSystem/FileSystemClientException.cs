using System;
using System.Diagnostics.CodeAnalysis;
using ProtonDrive.Shared.Extensions;

namespace ProtonDrive.Sync.Shared.FileSystem;

public class FileSystemClientException : Exception, IErrorCodeProvider
{
    public FileSystemClientException()
    {
    }

    public FileSystemClientException(string message)
        : base(message)
    {
    }

    public FileSystemClientException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }

    public FileSystemClientException(string message, FileSystemErrorCode errorCode, Exception? innerException = default)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }

    public FileSystemErrorCode ErrorCode { get; }

    /// <summary>
    /// Indicates whether the inner exception message is suitable for displaying in the UI
    /// </summary>
    public bool IsInnerExceptionMessageAuthoritative { get; init; }

    public bool TryGetRelevantFormattedErrorCode([MaybeNullWhen(false)] out string formattedErrorCode)
    {
        formattedErrorCode = ErrorCode.ToString();

        return true;
    }
}

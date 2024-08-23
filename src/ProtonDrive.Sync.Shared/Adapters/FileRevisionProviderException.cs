using System;
using System.Diagnostics.CodeAnalysis;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Sync.Shared.FileSystem;

namespace ProtonDrive.Sync.Shared.Adapters;

public class FileRevisionProviderException : Exception, IErrorCodeProvider
{
    public FileRevisionProviderException()
    {
    }

    public FileRevisionProviderException(string message)
        : base(message)
    {
    }

    public FileRevisionProviderException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }

    public FileRevisionProviderException(string message, FileSystemErrorCode errorCode, Exception? innerException = default)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }

    public FileSystemErrorCode ErrorCode { get; }

    public bool TryGetRelevantFormattedErrorCode([MaybeNullWhen(false)] out string formattedErrorCode)
    {
        formattedErrorCode = ErrorCode.ToString();

        return true;
    }
}

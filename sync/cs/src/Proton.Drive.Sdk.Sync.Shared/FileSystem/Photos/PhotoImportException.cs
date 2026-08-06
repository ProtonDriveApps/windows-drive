using System.Diagnostics.CodeAnalysis;
using Proton.Drive.Shared.Extensions;

namespace Proton.Drive.Sdk.Sync.Shared.FileSystem.Photos;

public class PhotoImportException : Exception, IFormattedErrorCodeProvider
{
    public PhotoImportException()
    {
    }

    public PhotoImportException(string message)
        : base(message)
    {
    }

    public PhotoImportException(string message, Exception innerException)
        : base(message, innerException)
    {
        if (innerException is IFileSystemErrorCodeProvider errorCodeProvider)
        {
            ErrorCode = GetPhotoImportErrorCode(errorCodeProvider.ErrorCode);
        }
    }

    public PhotoImportException(string message, PhotoImportErrorCode errorCode, Exception innerException)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }

    public PhotoImportErrorCode ErrorCode { get; protected set; }

    public virtual bool TryGetRelevantFormattedErrorCode([MaybeNullWhen(false)] out string formattedErrorCode)
    {
        formattedErrorCode = $"{ErrorCode}";
        return true;
    }

    private static PhotoImportErrorCode GetPhotoImportErrorCode(FileSystemErrorCode errorCode)
    {
        return errorCode switch
        {
            FileSystemErrorCode.ObjectNotFound => PhotoImportErrorCode.AlbumDoesNotExist,
            FileSystemErrorCode.TooManyChildren => PhotoImportErrorCode.MaximumNumberOfPhotosPerAlbumReached,
            _ => PhotoImportErrorCode.Unknown,
        };
    }
}

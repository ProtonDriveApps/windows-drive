using Proton.Drive.Sdk.Sync.Shared.FileSystem.Photos;

namespace Proton.Drive.Sdk.Sync.Client.FileUploading;

public sealed class PhotoFileSizeMismatchException : PhotoImportException
{
    public PhotoFileSizeMismatchException()
    {
    }

    public PhotoFileSizeMismatchException(string message)
        : base(message)
    {
    }

    public PhotoFileSizeMismatchException(string message, Exception exception)
        : base(message, exception)
    {
    }
}

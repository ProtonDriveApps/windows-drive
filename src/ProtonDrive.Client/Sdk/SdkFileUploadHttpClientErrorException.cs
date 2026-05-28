using ProtonDrive.Sync.Shared.FileSystem;

namespace ProtonDrive.Client.Sdk;

internal sealed class SdkFileUploadHttpClientErrorException : SdkFileTransferException
{
    private const string DefaultMessage = "Drive SDK file upload HTTP client error";

    private SdkFileUploadHttpClientErrorException(string message)
        : base(message)
    {
    }

    private SdkFileUploadHttpClientErrorException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    private SdkFileUploadHttpClientErrorException()
    {
    }

    public static SdkFileUploadHttpClientErrorException CreateInstance(Exception innerException, FileSystemErrorCode? errorCode)
    {
        var message = errorCode is not null ? $"{DefaultMessage}: {errorCode}" : DefaultMessage;
        return new SdkFileUploadHttpClientErrorException(message, innerException);
    }
}

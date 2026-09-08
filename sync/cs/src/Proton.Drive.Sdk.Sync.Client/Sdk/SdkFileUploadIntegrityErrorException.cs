namespace Proton.Drive.Sdk.Sync.Client.Sdk;

internal sealed class SdkFileUploadIntegrityErrorException : SdkFileTransferException
{
    private const string DefaultMessage = "Drive SDK file upload integrity error";

    private SdkFileUploadIntegrityErrorException(string message)
        : base(message)
    {
    }

    private SdkFileUploadIntegrityErrorException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    private SdkFileUploadIntegrityErrorException()
        : base(DefaultMessage)
    {
    }

    public static SdkFileUploadIntegrityErrorException Create(string message, Exception? innerException)
    {
        return innerException is null
            ? new SdkFileUploadIntegrityErrorException($"{DefaultMessage} ({message})")
            : new SdkFileUploadIntegrityErrorException($"{innerException.Message} ({message})", innerException);
    }
}

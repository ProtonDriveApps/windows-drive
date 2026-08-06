namespace Proton.Drive.Sdk.Sync.Client.Sdk;

internal sealed class SdkFileUploadUnknownErrorException : SdkFileTransferException
{
    private const string DefaultMessage = "Drive SDK file upload unknown error";

    private SdkFileUploadUnknownErrorException(string message)
        : base(message)
    {
    }

    private SdkFileUploadUnknownErrorException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    private SdkFileUploadUnknownErrorException()
        : base(DefaultMessage)
    {
    }

    public static SdkFileUploadUnknownErrorException Create(Exception? innerException)
    {
        return innerException is not null
            ? new SdkFileUploadUnknownErrorException(innerException.Message, innerException)
            : new SdkFileUploadUnknownErrorException();
    }
}

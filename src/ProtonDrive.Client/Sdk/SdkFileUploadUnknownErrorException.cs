namespace ProtonDrive.Client.Sdk;

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
    {
    }

    public static SdkFileUploadUnknownErrorException CreateInstance(Exception? innerException)
    {
        return innerException is not null
            ? new SdkFileUploadUnknownErrorException(DefaultMessage, innerException)
            : new SdkFileUploadUnknownErrorException(DefaultMessage);
    }
}

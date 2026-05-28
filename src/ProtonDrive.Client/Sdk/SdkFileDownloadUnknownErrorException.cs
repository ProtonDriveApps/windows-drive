namespace ProtonDrive.Client.Sdk;

internal sealed class SdkFileDownloadUnknownErrorException : SdkFileTransferException
{
    private const string DefaultMessage = "Drive SDK file download unknown error";

    private SdkFileDownloadUnknownErrorException(string message)
        : base(message)
    {
    }

    private SdkFileDownloadUnknownErrorException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    private SdkFileDownloadUnknownErrorException()
    {
    }

    public static SdkFileDownloadUnknownErrorException CreateInstance(Exception? innerException)
    {
        return innerException is not null
            ? new SdkFileDownloadUnknownErrorException(DefaultMessage, innerException)
            : new SdkFileDownloadUnknownErrorException(DefaultMessage);
    }
}

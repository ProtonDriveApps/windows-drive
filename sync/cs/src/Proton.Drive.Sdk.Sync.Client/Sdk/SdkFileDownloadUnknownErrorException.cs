namespace Proton.Drive.Sdk.Sync.Client.Sdk;

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
        : base(DefaultMessage)
    {
    }

    public static SdkFileDownloadUnknownErrorException Create(Exception? innerException)
    {
        return innerException is not null
            ? new SdkFileDownloadUnknownErrorException(innerException.Message, innerException)
            : new SdkFileDownloadUnknownErrorException();
    }
}

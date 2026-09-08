namespace Proton.Drive.Sdk.Sync.Client.Sdk;

internal sealed class SdkFileDownloadIntegrityErrorException : SdkFileTransferException
{
    private const string DefaultMessage = "Drive SDK file download integrity error";

    private SdkFileDownloadIntegrityErrorException(string message)
        : base(message)
    {
    }

    private SdkFileDownloadIntegrityErrorException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    private SdkFileDownloadIntegrityErrorException()
        : base(DefaultMessage)
    {
    }

    public static SdkFileDownloadIntegrityErrorException Create(Exception? innerException)
    {
        return innerException is not null
            ? new SdkFileDownloadIntegrityErrorException(innerException.Message, innerException)
            : new SdkFileDownloadIntegrityErrorException();
    }
}

namespace ProtonDrive.Client.Sdk;

internal abstract class SdkFileTransferException : Exception
{
    protected SdkFileTransferException(string message)
        : base(message)
    {
    }

    protected SdkFileTransferException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    protected SdkFileTransferException()
    {
    }
}

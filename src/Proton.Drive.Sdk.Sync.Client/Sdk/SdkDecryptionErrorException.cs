namespace Proton.Drive.Sdk.Sync.Client.Sdk;

internal sealed class SdkDecryptionErrorException : Exception
{
    private const string DefaultMessage = "Drive SDK decryption error";

    public SdkDecryptionErrorException()
        : base(DefaultMessage)
    {
    }

    public SdkDecryptionErrorException(string message)
        : base(message)
    {
    }

    public SdkDecryptionErrorException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

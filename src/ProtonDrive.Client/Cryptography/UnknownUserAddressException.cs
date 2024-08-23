using System.Security.Cryptography;

namespace ProtonDrive.Client.Cryptography;

internal class UnknownUserAddressException : CryptographicException
{
    public UnknownUserAddressException()
    {
    }

    public UnknownUserAddressException(string message, System.Exception innerException)
        : base(message, innerException)
    {
    }

    public UnknownUserAddressException(string addressId)
        : base($"Unknown address ID \"{addressId}\"")
    {
    }
}

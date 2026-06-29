using System.Security.Cryptography;

namespace Proton.Drive.Sdk.Sync.Client.Cryptography;

internal static class ExceptionExtensions
{
    public static CryptographicException ToDecryptionException(this Exception ex, string objectType, string objectId, string attributeType)
    {
        return new CryptographicException($"Failed to decrypt {attributeType} of {objectType} with ID={objectId}", ex);
    }
}

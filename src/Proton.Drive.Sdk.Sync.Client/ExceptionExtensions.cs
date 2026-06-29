using System.Security.Cryptography;
using Proton.Drive.Sdk.Sync.Client.Cryptography;

namespace Proton.Drive.Sdk.Sync.Client;

public static class ExceptionExtensions
{
    public static bool IsDriveClientException(this Exception exception) =>
        exception is ApiException or CryptographicException or KeyPassphraseUnavailableException;
}

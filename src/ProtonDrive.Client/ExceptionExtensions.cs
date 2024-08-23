using System;
using System.Security.Cryptography;
using ProtonDrive.Client.Cryptography;

namespace ProtonDrive.Client;

public static class ExceptionExtensions
{
    public static bool IsDriveClientException(this Exception exception) =>
        exception is ApiException or CryptographicException or KeyPassphraseUnavailableException;
}

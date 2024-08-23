using System;

namespace ProtonDrive.Client.Cryptography;

public class KeyPassphraseUnavailableException : Exception
{
    public KeyPassphraseUnavailableException(string message)
        : base(message)
    {
    }

    public KeyPassphraseUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public KeyPassphraseUnavailableException()
    {
    }
}

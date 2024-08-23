using System;
using System.Security.Cryptography;
using System.Text;
using ProtonDrive.Shared.Security.Cryptography;

namespace ProtonDrive.Sync.Windows.Security.Cryptography;

/// <inheritdoc cref="IDataProtectionProvider"/>
public class DataProtectionProvider : IDataProtectionProvider
{
    private static readonly byte[] Entropy =
    {
        0x42, 0xD0, 0xA5, 0x24, 0x15, 0x92, 0x3C, 0x78,
        0x7A, 0x1D, 0xFE, 0x11, 0x39, 0xF4, 0x5B, 0x72,
    };

    public string Protect(string data)
    {
        if (string.IsNullOrEmpty(data))
        {
            return data;
        }

        var unprotectedData = Encoding.UTF8.GetBytes(data);
        var protectedData = Protect(unprotectedData);

        return Convert.ToBase64String(protectedData.Span);
    }

    public ReadOnlyMemory<byte> Protect(ReadOnlyMemory<byte> data)
    {
        return ProtectedData.Protect(data.ToArray(), Entropy, DataProtectionScope.CurrentUser);
    }

    public string Unprotect(string data)
    {
        try
        {
            var protectedData = Convert.FromBase64String(data);
            var unprotectedData = Unprotect(protectedData);

            return Encoding.UTF8.GetString(unprotectedData.Span);
        }
        catch (FormatException)
        {
            throw new CryptographicException();
        }
        catch (ArgumentException)
        {
            throw new CryptographicException();
        }
    }

    public ReadOnlyMemory<byte> Unprotect(ReadOnlyMemory<byte> data)
    {
        return ProtectedData.Unprotect(data.ToArray(), Entropy, DataProtectionScope.CurrentUser);
    }
}

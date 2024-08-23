using System.Linq;
using System.Security;

namespace ProtonDrive.Shared.Security.Cryptography;

public static class StringToSecureStringConverter
{
    public static SecureString Convert(string text)
    {
        var result = text.Aggregate(
            new SecureString(),
            (s, c) =>
            {
                s.AppendChar(c);
                return s;
            });

        result.MakeReadOnly();

        return result;
    }
}

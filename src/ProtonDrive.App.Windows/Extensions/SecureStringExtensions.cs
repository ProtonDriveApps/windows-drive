using System.Runtime.InteropServices;
using System.Security;

namespace ProtonDrive.App.Windows.Extensions;

internal static class SecureStringExtensions
{
    public static string? ConvertToUnsecureString(this SecureString securePassword)
    {
        var unmanagedString = Marshal.SecureStringToGlobalAllocUnicode(securePassword);

        try
        {
            return Marshal.PtrToStringUni(unmanagedString);
        }
        finally
        {
            Marshal.ZeroFreeGlobalAllocUnicode(unmanagedString);
        }
    }
}

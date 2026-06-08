using System.Runtime.InteropServices;

namespace ProtonDrive.Sync.Windows.Interop;

internal static class Shlwapi
{
    [DllImport(Libraries.ShlwApi)]
    public static extern bool PathIsNetworkPath(string pszPath);
}

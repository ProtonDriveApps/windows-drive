using Vanara.PInvoke;

namespace Proton.Drive.Sdk.Sync.Windows.Shell;

public static class ShellExtensions
{
    public static unsafe void NotifyItemUpdate(string path)
    {
        fixed (char* pathPointer = path)
        {
            Shell32.SHChangeNotify(Shell32.SHCNE.SHCNE_UPDATEITEM, Shell32.SHCNF.SHCNF_PATHW, new nuint(pathPointer));
        }
    }
}

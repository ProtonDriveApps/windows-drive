using System.Runtime.InteropServices;

namespace ProtonDrive.Sync.Windows.Interop;

public static partial class Kernel32
{
    [DllImport(Libraries.Kernel32, ExactSpelling = true, SetLastError = true)]
    public static extern bool SetThreadErrorMode(uint dwNewMode, out uint lpOldMode);

    public const uint SEM_FAILCRITICALERRORS = 1;
}

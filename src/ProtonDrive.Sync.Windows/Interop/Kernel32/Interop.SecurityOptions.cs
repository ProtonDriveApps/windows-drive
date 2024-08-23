namespace ProtonDrive.Sync.Windows.Interop;

public static partial class Kernel32
{
    public static class SecurityOptions
    {
        internal const int SECURITY_SQOS_PRESENT = 0x00100000;
        internal const int SECURITY_ANONYMOUS = 0;
        internal const int SECURITY_IDENTIFICATION = 1 << 16;
        internal const int SECURITY_IMPERSONATION = 2 << 16;
        internal const int SECURITY_DELEGATION = 3 << 16;
    }
}

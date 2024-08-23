namespace ProtonDrive.Sync.Windows.Interop;

public static partial class Kernel32
{
    public enum READ_DIRECTORY_NOTIFY_INFORMATION_CLASS
    {
        ReadDirectoryNotifyInformation = 1,
        ReadDirectoryNotifyExtendedInformation = 2,
    }
}

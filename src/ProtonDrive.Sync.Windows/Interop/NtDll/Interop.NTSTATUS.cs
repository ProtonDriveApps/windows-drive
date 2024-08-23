namespace ProtonDrive.Sync.Windows.Interop;

public static partial class NtDll
{
    // https://docs.microsoft.com/en-us/windows-hardware/drivers/kernel/using-ntstatus-values
    // https://docs.microsoft.com/en-us/openspecs/windows_protocols/ms-erref/596a1078-e883-4972-9bbc-49e60bebca55
    public enum NTSTATUS : uint
    {
        STATUS_SUCCESS = 0x00000000,
        STATUS_SOME_NOT_MAPPED = 0x00000107,
        STATUS_INVALID_HANDLE = 0xC0000008,
        STATUS_NO_MORE_FILES = 0x80000006,
        STATUS_INVALID_PARAMETER = 0xC000000D,
        STATUS_FILE_NOT_FOUND = 0xC000000F,
        STATUS_NO_MEMORY = 0xC0000017,
        STATUS_ACCESS_DENIED = 0xC0000022,
        STATUS_OBJECT_NAME_NOT_FOUND = 0xC0000034,
        STATUS_SHARING_VIOLATION = 0xC0000043,
        STATUS_ACCOUNT_RESTRICTION = 0xC000006E,
        STATUS_NONE_MAPPED = 0xC0000073,
        STATUS_INSUFFICIENT_RESOURCES = 0xC000009A,
    }
}

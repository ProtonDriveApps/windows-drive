namespace ProtonDrive.Sync.Windows.Interop;

public static partial class NtDll
{
    // https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/ntifs/nf-ntifs-rtlqueryprocessplaceholdercompatibilitymode
    public enum PHCM : sbyte
    {
        PHCM_APPLICATION_DEFAULT = 0,
        PHCM_DISGUISE_PLACEHOLDER = 1,
        PHCM_EXPOSE_PLACEHOLDERS = 2,
        PHCM_ERROR_INVALID_PARAMETER = -1,
        PHCM_ERROR_NO_TEB = -2,
    }

    public static bool IsSuccess(this PHCM value) => (sbyte)value >= 0;

    public static bool IsFailure(this PHCM value) => !IsSuccess(value);
}

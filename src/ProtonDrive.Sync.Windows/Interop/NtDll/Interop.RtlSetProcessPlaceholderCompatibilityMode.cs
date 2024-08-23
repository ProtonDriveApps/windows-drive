using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace ProtonDrive.Sync.Windows.Interop;

[SuppressMessage("StyleCop.CSharp.NamingRules", "SA1313:Parameter names should begin with lower-case letter", Justification = "Win32 naming convention")]
public static partial class NtDll
{
    // https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/ntifs/nf-ntifs-rtlsetprocessplaceholdercompatibilitymode
    [DllImport(Libraries.NtDll, CharSet = CharSet.Unicode, ExactSpelling = true)]
    public static extern PHCM RtlSetProcessPlaceholderCompatibilityMode(PHCM Mode);
}

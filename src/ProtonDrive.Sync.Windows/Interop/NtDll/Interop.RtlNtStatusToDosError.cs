using System.Runtime.InteropServices;

namespace ProtonDrive.Sync.Windows.Interop;

public static partial class NtDll
{
    /// <summary>
    /// Converts the specified NTSTATUS code to its equivalent system error code.
    /// </summary>
    /// <param name="Status">The NTSTATUS code to be converted.</param>
    /// <returns>The corresponding system error code.</returns>
    /// <remarks>
    /// See https://msdn.microsoft.com/en-us/library/windows/desktop/ms680600(v=vs.85).aspx
    /// </remarks>
    [DllImport(Libraries.NtDll, ExactSpelling = true)]
    public static extern uint RtlNtStatusToDosError(
        NTSTATUS Status);
}

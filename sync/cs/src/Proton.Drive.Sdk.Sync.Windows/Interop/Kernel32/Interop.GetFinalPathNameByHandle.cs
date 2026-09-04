using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace Proton.Drive.Sdk.Sync.Windows.Interop;

public static partial class Kernel32
{
    /// <summary>
    /// Retrieves the final path for the specified file.
    /// </summary>
    /// <param name="hFile">A handle to a file or directory.</param>
    /// <param name="lpszFilePath">A pointer to a buffer that receives the path of hFile.</param>
    /// <param name="cchFilePath">The size of <paramref name="lpszFilePath"/>, in TCHARs. This value must include a NULL termination character.</param>
    /// <param name="dwFlags">The type of result to return. This parameter can be one of the following values.</param>
    /// <returns>
    /// <para>If the function succeeds, the return value is the length of the string received by <paramref name="lpszFilePath"/>, in TCHARs.
    /// This value does not include the size of the terminating null character.</para>
    /// <para>If the function fails because <paramref name="lpszFilePath"/> is too small to hold the string plus the terminating null character,
    /// the return value is the required buffer size, in TCHARs. This value includes the size of the terminating null character.</para>
    /// <para>If the function fails for any other reason, the return value is zero. To get extended error information, call <see cref="GetLastError"/>.</para>
    /// </returns>
    [DllImport(Libraries.Kernel32, EntryPoint = "GetFinalPathNameByHandleW", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern uint GetFinalPathNameByHandle(
        SafeFileHandle hFile,
        StringBuilder? lpszFilePath,
        uint cchFilePath,
        uint dwFlags);
}

using System.Runtime.InteropServices;

namespace Proton.Drive.Sdk.Sync.Windows.Interop;

public static partial class Kernel32
{
    /// <summary>
    /// Result of <c>GetFileInformationByHandleEx(FileIdInfo)</c>. The 128-bit file ID
    /// (<c>FILE_ID_128</c>, 16 bytes, little-endian) is split into its two 64-bit halves.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct FILE_ID_INFO
    {
        public ulong VolumeSerialNumber;
        public ulong FileIdLow;
        public ulong FileIdHigh;
    }
}

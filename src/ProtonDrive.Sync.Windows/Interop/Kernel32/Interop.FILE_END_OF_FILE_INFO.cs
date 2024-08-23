using System.Runtime.InteropServices;

namespace ProtonDrive.Sync.Windows.Interop;

public static partial class Kernel32
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct FILE_END_OF_FILE_INFO
    {
        public long EndOfFile;
    }
}

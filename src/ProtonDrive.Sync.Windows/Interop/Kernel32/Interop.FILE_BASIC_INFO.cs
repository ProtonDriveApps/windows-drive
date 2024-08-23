using System.Runtime.InteropServices;

namespace ProtonDrive.Sync.Windows.Interop;

public static partial class Kernel32
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct FILE_BASIC_INFO
    {
        public LONG_FILETIME CreationTime;
        public LONG_FILETIME LastAccessTime;
        public LONG_FILETIME LastWriteTime;
        public LONG_FILETIME ChangeTime;
        public uint FileAttributes;
    }
}

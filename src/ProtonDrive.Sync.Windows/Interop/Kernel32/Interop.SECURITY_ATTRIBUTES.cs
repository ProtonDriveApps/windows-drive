using System;
using System.Runtime.InteropServices;

namespace ProtonDrive.Sync.Windows.Interop;

public static partial class Kernel32
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct SECURITY_ATTRIBUTES
    {
        public uint nLength;
        public IntPtr lpSecurityDescriptor;
        public BOOL bInheritHandle;

        public bool InheritHandle
        {
            get => bInheritHandle != BOOL.FALSE;
            set => bInheritHandle = value ? BOOL.TRUE : BOOL.FALSE;
        }
    }
}

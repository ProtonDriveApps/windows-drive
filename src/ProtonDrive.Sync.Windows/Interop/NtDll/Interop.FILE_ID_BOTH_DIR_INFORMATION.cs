using System;
using System.Runtime.InteropServices;

namespace ProtonDrive.Sync.Windows.Interop;

public static partial class NtDll
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public unsafe struct FILE_ID_BOTH_DIR_INFORMATION
    {
        public uint NextEntryOffset;
        public uint FileIndex;
        public LONG_FILETIME CreationTime;
        public LONG_FILETIME LastAccessTime;
        public LONG_FILETIME LastWriteTime;
        public LONG_FILETIME ChangeTime;
        public ulong EndOfFile;
        public ulong AllocationSize;
        public uint FileAttributes;
        public int FileNameLength;
        public uint EaSize;
        public byte ShortNameLength;
        private fixed char _shortName[12];
        public ulong FileId;
        private fixed char _fileName[1];

        public ReadOnlySpan<char> ShortName
        {
            get { fixed (char* c = _shortName) return new ReadOnlySpan<char>(c, ShortNameLength / sizeof(char)); }
        }

        public ReadOnlySpan<char> FileName
        {
            get { fixed (char* c = _fileName) return new ReadOnlySpan<char>(c, FileNameLength / sizeof(char)); }
        }

        /// <summary>
        /// Gets the next info pointer or null if there are no more.
        /// </summary>
        public static FILE_ID_BOTH_DIR_INFORMATION* NextInfo(FILE_ID_BOTH_DIR_INFORMATION* info)
        {
            if (info == null)
                return null;

            uint offset = (*info).NextEntryOffset;
            if (offset == 0)
                return null;

            return (FILE_ID_BOTH_DIR_INFORMATION*)((byte*)info + offset);
        }
    }
}

using System;
using System.IO;
using System.Runtime.InteropServices;

namespace ProtonDrive.Sync.Windows.Interop;

public static partial class Kernel32
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct FILE_NOTIFY_EXTENDED_INFORMATION
    {
        public uint NextEntryOffset;
        public FileAction Action;
        public FILETIME CreationTime;
        public FILETIME LastModificationTime;
        public FILETIME LastChangeTime;
        public FILETIME LastAccessTime;
        public ulong AllocatedLength;
        public ulong FileSize;
        public FileAttributes FileAttributes;
        public uint ReparsePointTag;
        public long FileId;
        public long ParentFileId;
        public uint FileNameLength;
        private char _fileName;

        // Note that the file name is not null terminated
        internal unsafe ReadOnlySpan<char> FileName
        {
            get
            {
                fixed (char* c = &_fileName)
                {
                    return new ReadOnlySpan<char>(c, (int)FileNameLength / sizeof(char));
                }
            }
        }

        /// <summary>
        /// Gets the next info pointer or null if there are no more.
        /// </summary>
        public static unsafe FILE_NOTIFY_EXTENDED_INFORMATION* NextInfo(FILE_NOTIFY_EXTENDED_INFORMATION* info)
        {
            if (info == null)
            {
                return null;
            }

            uint offset = (*info).NextEntryOffset;
            if (offset == 0)
            {
                return null;
            }

            return (FILE_NOTIFY_EXTENDED_INFORMATION*)((byte*)info + offset);
        }
    }
}

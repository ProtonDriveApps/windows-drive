using System.Runtime.InteropServices;

namespace Proton.Drive.Sdk.Sync.Windows.Interop;

public static partial class Kernel32
{
    /// <summary>
    /// Entry returned by <c>ReadDirectoryChangesExW</c> with <c>ReadDirectoryNotifyInformation</c>.
    /// Unlike <see cref="FILE_NOTIFY_EXTENDED_INFORMATION"/> it carries no file IDs.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct FILE_NOTIFY_INFORMATION
    {
        public uint NextEntryOffset;
        public FileAction Action;
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
        public static unsafe FILE_NOTIFY_INFORMATION* NextInfo(FILE_NOTIFY_INFORMATION* info)
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

            return (FILE_NOTIFY_INFORMATION*)((byte*)info + offset);
        }
    }
}

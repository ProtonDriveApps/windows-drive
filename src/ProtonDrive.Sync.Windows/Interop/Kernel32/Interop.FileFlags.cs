// ReSharper disable InconsistentNaming

using System.Diagnostics.CodeAnalysis;

namespace ProtonDrive.Sync.Windows.Interop;

[SuppressMessage("StyleCop.CSharp.NamingRules", "SA1310:Field names should not contain underscore", Justification = "Win32 naming convention")]
public static partial class Kernel32
{
    public static class FileFlags
    {
        internal const uint FILE_FLAG_OPEN_NO_RECALL = 0x00100000;
        internal const uint FILE_FLAG_OPEN_REPARSE_POINT = 0x00200000;
        internal const uint FILE_FLAG_SESSION_AWARE = 0x00800000;
        internal const uint FILE_FLAG_POSIX_SEMANTICS = 0x01000000;
        internal const uint FILE_FLAG_BACKUP_SEMANTICS = 0x02000000;
        internal const uint FILE_FLAG_DELETE_ON_CLOSE = 0x04000000;
        internal const uint FILE_FLAG_SEQUENTIAL_SCAN = 0x08000000;
        internal const uint FILE_FLAG_RANDOM_ACCESS = 0x10000000;
        internal const uint FILE_FLAG_NO_BUFFERING = 0x20000000;
        internal const uint FILE_FLAG_OVERLAPPED = 0x40000000;
        internal const uint FILE_FLAG_WRITE_THROUGH = 0x80000000;
    }
}

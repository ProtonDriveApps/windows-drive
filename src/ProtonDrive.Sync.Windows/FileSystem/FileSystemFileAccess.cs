using System;
using ProtonDrive.Sync.Windows.Interop;

namespace ProtonDrive.Sync.Windows.FileSystem;

[Flags]
public enum FileSystemFileAccess : uint
{
    None = default,

    /// <summary>
    /// For a file, the right to read data from the file.
    /// For a directory, the right to list the contents.
    /// </summary>
    ReadData = Kernel32.DesiredAccess.FILE_READ_DATA | Kernel32.DesiredAccess.FILE_LIST_DIRECTORY,

    /// <summary>
    /// For a file, the right to write data to the file.
    /// For a directory, the right to create a file in a directory.
    /// </summary>
    WriteData = Kernel32.DesiredAccess.FILE_WRITE_DATA | Kernel32.DesiredAccess.FILE_ADD_FILE,

    /// <summary>
    /// For a file, the right to append data to a file. <see cref="WriteData"/> is needed
    /// to overwrite existing data.
    /// For a directory, the right to create a sub directory.
    /// </summary>
    AppendData = Kernel32.DesiredAccess.FILE_APPEND_DATA | Kernel32.DesiredAccess.FILE_ADD_SUBDIRECTORY,

    /// <summary>
    /// The right to read extended attributes.
    /// </summary>
    ReadExtendedAttributes = Kernel32.DesiredAccess.FILE_READ_EA,

    /// <summary>
    /// The right to write extended attributes.
    /// </summary>
    WriteExtendedAttributes = Kernel32.DesiredAccess.FILE_WRITE_EA,

    /// <summary>
    /// The right to execute the file.
    /// </summary>
    /// <remarks>
    /// Directory version of this flag is <see cref="TraverseDirectory"/>.
    /// </remarks>
    ExecuteFile = Kernel32.DesiredAccess.FILE_EXECUTE,

    /// <summary>
    /// For a directory, the right to traverse the directory.
    /// </summary>
    /// <remarks>
    /// File version of this flag is <see cref="ExecuteFile"/>.
    /// </remarks>
    TraverseDirectory = Kernel32.DesiredAccess.FILE_TRAVERSE,

    /// <summary>
    /// For a directory, the right to delete a directory and all
    /// the files it contains, including read-only files.
    /// </summary>
    DeleteChildren = Kernel32.DesiredAccess.FILE_DELETE_CHILD,

    /// <summary>
    /// The right to read attributes. It is automatically added to any call to
    /// <see cref="Kernel32.CreateFile"/>.
    /// </summary>
    ReadAttributes = Kernel32.DesiredAccess.FILE_READ_ATTRIBUTES,

    /// <summary>
    /// The right to write attributes.
    /// </summary>
    WriteAttributes = Kernel32.DesiredAccess.FILE_WRITE_ATTRIBUTES,

    /// <summary>
    /// The right to delete the object.
    /// </summary>
    Delete = Kernel32.DesiredAccess.DELETE,

    /// <summary>
    /// All standard and specific rights.
    /// </summary>
    AllAccess = Kernel32.DesiredAccess.FILE_ALL_ACCESS,

    /// <summary>
    /// Generic read.
    /// </summary>
    Read = Kernel32.DesiredAccess.FILE_GENERIC_READ,

    /// <summary>
    /// Generic write.
    /// </summary>
    Write = Kernel32.DesiredAccess.FILE_GENERIC_WRITE,

    /// <summary>
    /// Generic execute.
    /// </summary>
    Execute = Kernel32.DesiredAccess.FILE_GENERIC_EXECUTE,

    /// <summary>
    /// Generic read and write.
    /// </summary>
    ReadWrite = Read | Write,
}

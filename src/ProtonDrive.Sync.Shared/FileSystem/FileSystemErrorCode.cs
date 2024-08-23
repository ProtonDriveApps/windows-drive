namespace ProtonDrive.Sync.Shared.FileSystem;

public enum FileSystemErrorCode
{
    /// <summary>
    /// Not specified error.
    /// </summary>
    Unknown,

    /// <summary>
    /// An attempt to access a file system object by path that does not exist on the file system failed.
    /// Applicable to file systems where file system objects are opened by path.
    /// </summary>
    /// <remarks>
    /// The ObjectId property contains the expected identity value of the file system object
    /// not found at the specified path.
    /// <para>
    /// On Proton Drive, file system objects are opened by identity. The <see cref="ObjectNotFound"/>
    /// is returned when the file system object with the specified identity cannot be found.
    /// </para>
    /// </remarks>
    PathNotFound,

    /// <summary>
    /// An attempt to access a file system object by identity that does not exist on the file system failed.
    /// Applicable to file systems where file system objects are opened by identity.
    /// </summary>
    /// <remarks>
    /// The ObjectId property contains the identity value of the not existing file system object.
    /// <para>
    /// On local file systems, file system objects are opened by path. The <see cref="PathNotFound"/>
    /// is returned when the file system object with the specified path cannot be found.
    /// </para>
    /// </remarks>
    ObjectNotFound,

    /// <summary>
    /// Part of a file or directory path cannot be found on the file system.
    /// Applicable to file systems where file system objects are opened by path.
    /// </summary>
    /// <remarks>
    /// The ObjectId property contains the identity value of the file system object
    /// whose part of a path cannot be found.
    /// </remarks>
    DirectoryNotFound,

    /// <summary>
    /// The file system object is already opened with not compatible sharing mode.
    /// </summary>
    /// <remarks>
    /// The ObjectId property contains the identity value of the file system object.
    /// </remarks>
    SharingViolation,

    /// <summary>
    /// The user accessing the file system has not been granted the required permissions
    /// for the specified access type.
    /// </summary>
    /// <remarks>
    /// The ObjectId property contains the identity value of the file system object
    /// the access to which has failed.
    /// </remarks>
    UnauthorizedAccess,

    /// <summary>
    /// An attempt to create, rename or move an object with the name of already existing object at the
    /// same parent directory failed.
    /// </summary>
    /// <remarks>
    /// The ObjectId property contains the identity value of the file system object
    /// that failed to rename or move.
    /// </remarks>
    DuplicateName,

    /// <summary>
    /// An attempt to create, rename or move an object with/to an invalid name failed.
    /// </summary>
    /// <remarks>
    /// The ObjectId property contains the identity value of the file system object
    /// that failed to rename or move.
    /// </remarks>
    InvalidName,

    /// <summary>
    /// The identity of the file system object or parent directory does not match the expected value.
    /// Applicable to file systems where file system objects are opened by path.
    /// </summary>
    /// <remarks>
    /// The ObjectId property contains the expected identity value of the file system object.
    /// </remarks>
    IdentityMismatch,

    /// <summary>
    /// The metadata of the file system object does not match the expected.
    /// </summary>
    /// <remarks>
    /// The ObjectId property contains the identity value of the file system object.
    /// </remarks>
    MetadataMismatch,

    /// <summary>
    /// Max allowed number of folder children is reached.
    /// </summary>
    TooManyChildren,

    /// <summary>
    /// The request timed out.
    /// </summary>
    TimedOut,

    /// <summary>
    /// The file system is offline.
    /// </summary>
    Offline,

    /// <summary>
    /// There is not enough available space to finalize the file upload.
    /// </summary>
    FreeSpaceExceeded,

    /// <summary>
    /// Operation was cancelled.
    /// </summary>
    Cancelled,

    /// <summary>
    /// The file system does not support path based access.
    /// Please provide the file system object identity value.
    /// </summary>
    PathBasedAccessNotSupported,

    /// <summary>
    /// The file or directory must be a placeholder and its content is not ready to be consumed by the user application,
    /// though it may or may not be fully present locally.
    /// </summary>
    Partial,

    /// <summary>
    /// The file system failed an integrity verification while writing file contents.
    /// </summary>
    IntegrityFailure,

    /// <summary>
    /// File opened by other process has changed after the upload has started
    /// </summary>
    TransferAbortedDueToFileChange,

    /// <summary>
    /// Indicates that the file content has been modified recently.
    /// </summary>
    /// <remarks>
    /// This member is set when the difference between the current time and the file's last modified time is less than the configured threshold.
    /// </remarks>
    LastWriteTimeTooRecent,
}

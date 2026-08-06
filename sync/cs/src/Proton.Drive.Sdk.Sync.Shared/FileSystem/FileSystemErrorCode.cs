namespace Proton.Drive.Sdk.Sync.Shared.FileSystem;

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
    /// The file system failed an integrity verification while reading or writing file contents.
    /// </summary>
    IntegrityFailure,

    /// <summary>
    /// File opened by other process has changed after the upload has started
    /// </summary>
    TransferAbortedDueToFileChange,

    /// <summary>
    /// The file content has been modified recently.
    /// </summary>
    /// <remarks>
    /// This member is set when the difference between the current time and the file's last modified time is less than the configured threshold.
    /// </remarks>
    LastWriteTimeTooRecent,

    /// <summary>
    /// A local file that was individually shared with the user is missing from its expected location.
    /// </summary>
    /// <remarks>
    /// Applies to roots representing files shared with me. For example, if a local file has been renamed, moved, or deleted,
    /// it syncs as a remote deletion, resulting in this error.
    /// </remarks>
    MissingIndividuallySharedFile,

    /// <summary>
    /// The file system root is read-only.
    /// </summary>
    /// <remarks>
    /// Applies to remote roots representing files and folders shared with me with viewer permissions.
    /// </remarks>
    ReadOnlyRoot,

    /// <summary>
    /// The file is read-only.
    /// </summary>
    ReadOnlyFile,

    /// <summary>
    /// Failure due to rate limiting on the API side.
    /// </summary>
    RateLimited,

    /// <summary>
    /// Network error that is purely on the client side.
    /// For example, client is offline.
    /// </summary>
    NetworkError,

    /// <summary>
    /// Network errors that might be blamed on our server.
    /// For example, timeout, API not reachable but otherwise client has connection, API responding with non-JSON output.
    /// </summary>
    ServerError,

    /// <summary>
    /// The file cannot be read because the file is not hydrated and the cloud file provider (i.e. OneDrive) is not running.
    /// </summary>
    CloudFileProviderNotRunning,

    /// <summary>
    /// This error can occur in the following scenarios:
    /// - Reading from or writing to a corrupted file or accessing external storage (e.g., USB drives, CDs/DVDs) with data integrity issues.
    /// - Working with a FileStream, MemoryMappedFile, or another I/O stream that relies on corrupted or invalid data.
    /// </summary>
    CyclicRedundancyCheck,

    /// <summary>
    /// The main photo associated with the related photo already belongs to an album,
    /// therefore the related photo cannot be uploaded.
    /// </summary>
    MainPhotoAlreadyInAlbum,

    /// <summary>
    /// Usually indicates a hardware problem with the drive (such as bad sectors, a failing disk, or an unstable external or network device)
    /// rather than a fault in the application.
    /// </summary>
    /// <remarks>
    /// For example, on Windows, the storage device could report ERROR_IO_DEVICE (0x8007045D) while writing file contents,
    /// preventing the operation from completing.
    /// </remarks>
    LocalStorageError,
}

namespace ProtonDrive.Shared.IO;

public enum FileSystemAttributes
{
    None = 0,

    /// <summary>
    /// The file system supports case-sensitive file names.
    /// </summary>
    CaseSensitiveSearch = 1,

    /// <summary>
    /// The file system preserves the case of file names when it places a name on disk.
    /// </summary>
    CasePreservedNames = 2,

    /// <summary>
    /// The file system supports Unicode in file names as they appear on disk.
    /// </summary>
    UnicodeOnDisk = 4,

    /// <summary>
    /// The file system preserves and enforces access control lists (ACL).
    /// </summary>
    PersistentACLs = 8,

    /// <summary>
    /// The file system supports file-based compression.
    /// </summary>
    FileCompression = 0x10,

    /// <summary>
    /// The file system supports disk quotas.
    /// </summary>
    VolumeQuotas = 0x20,

    /// <summary>
    /// The file system supports sparse files.
    /// </summary>
    SupportsSparseFiles = 0x40,

    /// <summary>
    /// The file system supports re-parse points.
    /// </summary>
    SupportsReparsePoints = 0x80,

    /// <summary>
    /// The file system supports remote storage.
    /// </summary>
    SupportsRemoteStorage = 0x100,

    /// <summary>
    /// The specified volume is a compressed volume, for example, a DoubleSpace volume.
    /// </summary>
    VolumeIsCompressed = 0x8000,

    /// <summary>
    /// The file system supports object identifiers.
    /// </summary>
    SupportsObjectIDs = 0x10000,

    /// <summary>
    /// The file system supports the Encrypted File System (EFS).
    /// </summary>
    SupportsEncryption = 0x20000,

    /// <summary>
    /// The file system supports named streams.
    /// </summary>
    NamedStreams = 0x40000,

    /// <summary>
    /// The specified volume is read-only.
    /// </summary>
    ReadOnlyVolume = 0x80000,

    /// <summary>
    /// The volume supports a single sequential write.
    /// </summary>
    SequentialWriteOnce = 0x100000,

    /// <summary>
    /// The volume supports transactions.
    /// </summary>
    SupportsTransactions = 0x200000,

    /// <summary>
    /// The specified volume supports hard links. For more information, see Hard Links and Junctions.
    /// </summary>
    SupportsHardLinks = 0x400000,

    /// <summary>
    /// The specified volume supports extended attributes. An extended attribute is a piece of
    /// application-specific metadata that an application can associate with a file and is not part
    /// of the file's data.
    /// </summary>
    SupportsExtendedAttributes = 0x800000,

    /// <summary>
    /// The file system supports open by FileID. For more information, see FILE_ID_BOTH_DIR_INFO.
    /// </summary>
    SupportsOpenByFileId = 0x1000000,

    /// <summary>
    /// The specified volume supports update sequence number (USN) journals. For more information,
    /// see Change Journal Records.
    /// </summary>
    SupportsUsnJournal = 0x2000000,
}

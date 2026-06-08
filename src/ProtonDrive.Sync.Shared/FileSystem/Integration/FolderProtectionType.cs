namespace ProtonDrive.Sync.Shared.FileSystem.Integration;

public enum FolderProtectionType
{
    /// <summary>
    /// For folders which contain protected subfolders
    /// </summary>
    Ancestor,

    /// <summary>
    /// For folders which contain protected subfolders and non-protected files
    /// </summary>
    AncestorWithFiles,

    /// <summary>
    /// For folders which do not contain any protected subfolders
    /// </summary>
    Leaf,

    /// <summary>
    /// For folders which should not be modified by the user
    /// </summary>
    ReadOnly,
}

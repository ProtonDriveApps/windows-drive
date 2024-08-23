namespace ProtonDrive.App.SystemIntegration;

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
    /// For "Shared with me" folder which contains shared files
    /// which must be protected to prevent users from deleting or renaming them.
    /// </summary>
    AncestorWithSharedWithMeFiles,
}

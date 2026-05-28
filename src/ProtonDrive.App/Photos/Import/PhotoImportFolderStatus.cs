namespace ProtonDrive.App.Photos.Import;

public enum PhotoImportFolderStatus
{
    /// <summary>
    /// Import didn't execute yet.
    /// Once import starts, status never returns back to <see cref="PhotoImportFolderStatus.NotStarted"/>.
    /// </summary>
    NotStarted = 0,

    /// <summary>
    /// Import is currently executing.
    /// </summary>
    Importing = 1,

    /// <summary>
    /// Import succeeded without errors.
    /// There could be no files imported or no files found to import.
    /// </summary>
    Succeeded = 2,

    /// <summary>
    /// Import was interrupted.
    /// </summary>
    Interrupted = 3,

    /// <summary>
    /// Import failed.
    /// There could be successfully imported files.
    /// </summary>
    Failed = 4,

    /// <summary>
    /// Folder validation failed.
    /// Value is generated in the view model, it does not appear in the <see cref="PhotoImportFolderState"/>.
    /// </summary>
    ValidationFailed = 5,
}

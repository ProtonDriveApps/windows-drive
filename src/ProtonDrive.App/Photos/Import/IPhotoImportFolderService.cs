using ProtonDrive.App.Mapping;

namespace ProtonDrive.App.Photos.Import;

public interface IPhotoImportFolderService
{
    /// <summary>
    /// Validates local folder applicability for importing photos from.
    /// </summary>
    /// <remarks>
    /// It checks whether:
    /// <list type="bullet">
    /// <item>The folder exists</item>
    /// <item>The folder path does not overlap with other forders</item>
    /// </list>
    /// </remarks>
    /// <param name="path">The path of a photo import folder to validate.</param>
    /// <returns>The validation result.</returns>
    SyncFolderValidationResult ValidateFolder(string path);

    /// <summary>
    /// Adds the specified local folder to import photos from it.
    /// </summary>
    /// <remarks>
    /// No validation of folder is attempted, it will be performed by photos import.
    /// </remarks>
    /// <param name="path">Local folder path.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous adding a folder operation.
    /// It's optional to await it as the method doesn't raise expected exceptions.
    /// </returns>
    Task AddFolderAsync(string path, CancellationToken cancellationToken);

    /// <summary>
    /// Persists updates to the specified photo import folder.
    /// </summary>
    /// <param name="folder">The photo import folder to persist updates.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous persisting updates operation.</returns>
    Task UpdateFolderAsync(PhotoImportFolderState folder, CancellationToken cancellationToken);

    /// <summary>
    /// Resets the photo import folder status to trigger a new import attempt.
    /// </summary>
    /// <param name="folder">The local folder to retry import from</param>.
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous adding mapping operation.</returns>
    Task RetryImportAsync(PhotoImportFolderState folder, CancellationToken cancellationToken);

    /// <summary>
    /// Removes the photo import folder.
    /// </summary>
    /// <param name="folder">The photo import folder to remove.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous removing folder operation.
    /// It's optional to await it as the method doesn't raise expected exceptions.
    /// </returns>
    Task RemoveFolderAsync(PhotoImportFolderState folder, CancellationToken cancellationToken);
}

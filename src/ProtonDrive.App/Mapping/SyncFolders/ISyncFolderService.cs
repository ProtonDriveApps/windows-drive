using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ProtonDrive.App.Mapping.SyncFolders;

public interface ISyncFolderService
{
    /// <summary>
    /// Validates account root folder candidate.
    /// </summary>
    /// <remarks>
    /// Account root folder contains cloud files folder ("My files") and foreign devices
    /// folder ("Other computers").
    /// </remarks>
    /// <param name="path">Account root folder path.</param>
    /// <returns>The result of the validation.</returns>
    SyncFolderValidationResult ValidateAccountRootFolder(string path);

    /// <summary>
    /// Validates local folder applicability for syncing.
    /// Checks if a folder path does not overlap with a provided set of other paths,
    /// whether the folder is on a supported volume.
    /// </summary>
    /// <param name="path">The path of a folder to validate</param>
    /// <param name="otherPaths">List of folder paths to be validated against</param>
    /// <returns>The result of the validation.</returns>
    SyncFolderValidationResult ValidateSyncFolder(string path, IReadOnlySet<string> otherPaths);

    /// <summary>
    /// Changes account root folder to the new one.
    /// <remarks>
    /// The old cloud files mapping is deleted and the new one is created.
    /// No validation of local folder is attempted, it will be performed by sync folder mapping setup.
    /// </remarks>
    /// </summary>
    /// <param name="localPath">New account root folder paths.</param>
    /// <returns>
    /// A task that represents the asynchronous account root folder change operation.
    /// It's optional to await it as the method doesn't raise expected exceptions.
    /// </returns>
    Task SetAccountRootFolderAsync(string localPath);

    /// <summary>
    /// Adds specified host device sync folders (known or arbitrary folders chosen by the user).
    /// </summary>
    /// <remarks>
    /// No validation of folders to add is attempted, it will be performed by sync folder mapping setup.
    /// </remarks>
    /// <param name="localPaths">The collection of local folder paths to add.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous adding sync folders operation.
    /// It's optional to await it as the method doesn't raise expected exceptions.
    /// </returns>
    Task AddHostDeviceFoldersAsync(ICollection<string> localPaths, CancellationToken cancellationToken);

    /// <summary>
    /// Removes specified host device sync folder.
    /// </summary>
    /// <param name="syncFolder">The sync folder to remove.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous removing sync folder operation.
    /// It's optional to await it as the method doesn't raise expected exceptions.
    /// </returns>
    Task RemoveHostDeviceFolderAsync(SyncFolder syncFolder, CancellationToken cancellationToken);
}

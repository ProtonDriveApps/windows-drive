using System.Threading;
using System.Threading.Tasks;
using ProtonDrive.Client.Shares.SharedWithMe;

namespace ProtonDrive.App.Mapping;

public interface ISharedWithMeMappingService
{
    /// <summary>
    /// Adds a mapping for the specified shared with me item to enable syncing it.
    /// </summary>
    /// <remarks>
    /// No validation of shared with me item is attempted, it will be performed by sync folder mapping setup.
    /// </remarks>
    /// <param name="item">The shared with me item.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous adding mapping operation.
    /// It's optional to await it as the method doesn't raise expected exceptions.
    /// </returns>
    Task AddSharedWithMeItemAsync(SharedWithMeItem item, CancellationToken cancellationToken);

    /// <summary>
    /// Removes mapping of the specified shared with me item to disable syncing it.
    /// </summary>
    /// <param name="item">The shared with me sync folder to remove.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous removing mapping operation.
    /// It's optional to await it as the method doesn't raise expected exceptions.
    /// </returns>
    Task RemoveSharedWithMeItemAsync(SharedWithMeItem item, CancellationToken cancellationToken);
}

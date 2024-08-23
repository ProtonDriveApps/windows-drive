using System.Threading;
using System.Threading.Tasks;

namespace ProtonDrive.App.Mapping;

public interface IMappingRegistry
{
    /// <summary>
    /// Obtains the interface for inspecting, adding, and removing mappings.
    /// Saves changes to the repository and notifies about mapping changes when disposing the obtained object.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A <see cref="IUpdatableMappings"/> that allows inspecting, adding, and removing mappings.
    /// Must be disposed upon finishing using it.
    /// </returns>
    Task<IUpdatableMappings> GetMappingsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Saves mappings to the repository. Use it only for saving changes to mapping properties. Changes to set
    /// of mappings are automatically saved when <see cref="IUpdatableMappings"/> is disposed.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous save operation.</returns>
    Task SaveAsync(CancellationToken cancellationToken);
}

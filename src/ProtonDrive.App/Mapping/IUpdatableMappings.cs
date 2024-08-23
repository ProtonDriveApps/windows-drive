using System;
using System.Collections.Generic;
using ProtonDrive.App.Settings;

namespace ProtonDrive.App.Mapping;

public interface IUpdatableMappings : IDisposable
{
    /// <summary>
    /// Obtains a snapshot collection of active mappings.
    /// </summary>
    /// <returns>Collection of active mappings.</returns>
    IReadOnlyCollection<RemoteToLocalMapping> GetActive();

    /// <summary>
    /// Adds new mapping to the set of active mappings and generates unique ID for it.
    /// </summary>
    /// <param name="mapping">Mapping to add.</param>
    void Add(RemoteToLocalMapping mapping);

    /// <summary>
    /// Deletes active mapping by changing its status to <see cref="MappingStatus.Deleted"/>
    /// and moving it to the set of deleted mappings.
    /// </summary>
    /// <param name="mapping">Mapping to delete.</param>
    void Delete(RemoteToLocalMapping mapping);

    /// <summary>
    /// Removes torn down mapping.
    /// </summary>
    /// <param name="mapping">Mapping to remove.</param>
    void Remove(RemoteToLocalMapping mapping);

    /// <summary>
    /// Removes all mappings, both active and deleted ones.
    /// Used when switching user account.
    /// </summary>
    void Clear();

    /// <summary>
    /// Saves changes and notifies through <see cref="IMappingsAware"/>.
    /// <para>Skips saving and notification if there were no changes to mappings through this interface.</para>
    /// </summary>
    /// <remarks>
    /// Explicit saving and notification is not required, as same happens implicitly when disposing this object.
    /// Can be used to prevent re-entrance when same module both changes mappings and reacts to mapping changes.
    /// </remarks>
    void SaveAndNotify();
}

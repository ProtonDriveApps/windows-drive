using System;
using ProtonDrive.Shared.Repository;
using ProtonDrive.Sync.Shared.FileSystem;

namespace ProtonDrive.Client;

public interface IRemoteEventLogClientFactory
{
    IEventLogClient<string> CreateClientForShare(string shareId, IRepository<string> anchorIdRepository, TimeSpan pollInterval);
    IEventLogClient<string> CreateClientForVolume(string volumeId, IRepository<string> anchorIdRepository, TimeSpan pollInterval);
}

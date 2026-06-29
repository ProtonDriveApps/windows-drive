using Proton.Drive.Shared.Repository;

namespace Proton.Drive.Sdk.Sync.Shared.FileSystem;

public interface IRemoteEventLogClientFactory
{
    IEventLogClient<string> CreateClientForShare(string shareId, IRepository<string> anchorIdRepository, TimeSpan pollInterval);
    IEventLogClient<string> CreateClientForVolume(string volumeId, IRepository<string> anchorIdRepository, TimeSpan pollInterval);
}

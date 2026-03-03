using ProtonDrive.App.Settings;
using ProtonDrive.DataAccess.Databases;
using ProtonDrive.Sync.Adapter;
using ProtonDrive.Sync.Agent.Health;
using ProtonDrive.Sync.Shared;
using ProtonDrive.Sync.Shared.Adapters;

namespace ProtonDrive.App.Health;

public interface IFileConsistencyGuardFactory
{
    IFileConsistencyGuard Create(
        IReadOnlyCollection<RemoteToLocalMapping> mappings,
        IFileRevisionProvider<long> localFileRevisionProvider,
        IFileRevisionProvider<long> remoteFileRevisionProvider,
        ITransactedScheduler localAdapterSyncScheduler,
        LocalAdapterDatabase localAdapterDatabase,
        ITransactedScheduler remoteAdapterSyncScheduler,
        RemoteAdapterDatabase remoteAdapterDatabase,
        IManagedAdapter<long> remoteAdapter);
}

using Proton.Drive.Sdk.Sync.Adapter;
using Proton.Drive.Sdk.Sync.Agent.Settings;
using Proton.Drive.Sdk.Sync.DataAccess.Databases;
using Proton.Drive.Sdk.Sync.Shared;
using Proton.Drive.Sdk.Sync.Shared.Adapters;

namespace Proton.Drive.Sdk.Sync.Agent.Health.FileConsistency;

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

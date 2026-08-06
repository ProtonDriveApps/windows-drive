using Proton.Drive.Sdk.Sync.Shared.FileSystem;

namespace Proton.Drive.Sdk.Sync.Windows.FileSystem.Client;

public sealed class LocalEventLogClientFactory : ILocalEventLogClientFactory
{
    public IRootableEventLogClient<long> Create()
    {
        return new EventLogClient();
    }
}

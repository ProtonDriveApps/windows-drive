namespace Proton.Drive.Sdk.Sync.Shared.FileSystem;

public interface ILocalEventLogClientFactory
{
    IRootableEventLogClient<long> Create();
}

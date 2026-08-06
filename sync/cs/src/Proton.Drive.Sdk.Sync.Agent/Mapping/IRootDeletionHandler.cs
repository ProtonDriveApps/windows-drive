namespace Proton.Drive.Sdk.Sync.Agent.Mapping;

internal interface IRootDeletionHandler
{
    void HandleRootDeletion(IEnumerable<int> rootIds);
}

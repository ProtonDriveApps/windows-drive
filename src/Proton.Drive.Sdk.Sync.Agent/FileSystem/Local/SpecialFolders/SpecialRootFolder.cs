using Proton.Drive.Sdk.Sync.Shared.FileSystem;

namespace Proton.Drive.Sdk.Sync.Agent.FileSystem.Local.SpecialFolders;

internal sealed class SpecialRootFolder<TId> : ISpecialFolder<TId>
    where TId : IEquatable<TId>
{
    private readonly TId _rootId;

    public SpecialRootFolder(TId rootId)
    {
        _rootId = rootId;
    }

    public Task<NodeInfo<TId>> GetOrCreate(CancellationToken cancellationToken)
    {
        return Task.FromResult(NodeInfo<TId>.Directory().WithId(_rootId));
    }
}

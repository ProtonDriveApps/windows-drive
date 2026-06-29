using Proton.Drive.Sdk.Sync.Shared.FileSystem;

namespace Proton.Drive.Sdk.Sync.Agent.FileSystem.Local.SpecialFolders;

internal interface ISpecialFolder<TId>
    where TId : IEquatable<TId>
{
    Task<NodeInfo<TId>> GetOrCreate(CancellationToken cancellationToken);
}

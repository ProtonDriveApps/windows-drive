using Proton.Drive.Sdk.Sync.Shared.Trees.FileSystem;
using Proton.Drive.Sdk.Sync.Shared.Trees.Operations;

namespace Proton.Drive.Sdk.Sync.Shared.Trees.Changes;

public interface IDetectedTreeChanges<TId>
    where TId : IEquatable<TId>
{
    void Add(Operation<FileSystemNodeModel<TId>> operation);
    bool Contains(TId id);
}

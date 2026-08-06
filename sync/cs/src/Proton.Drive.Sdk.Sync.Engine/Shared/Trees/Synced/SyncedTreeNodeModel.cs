using Proton.Drive.Sdk.Sync.Shared.Trees.FileSystem;

namespace Proton.Drive.Sdk.Sync.Engine.Shared.Trees.Synced;

public class SyncedTreeNodeModel<TId> : AltIdentifiableFileSystemNodeModel<TId, TId>
    where TId : IEquatable<TId>
{
}

using System;
using ProtonDrive.Sync.Shared.Trees.FileSystem;

namespace ProtonDrive.Sync.Engine.Shared.Trees.Synced;

public class SyncedTreeNodeModel<TId> : AltIdentifiableFileSystemNodeModel<TId, TId>
    where TId : IEquatable<TId>
{
}

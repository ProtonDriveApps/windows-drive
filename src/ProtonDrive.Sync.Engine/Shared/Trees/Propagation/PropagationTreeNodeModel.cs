using System;
using ProtonDrive.Sync.Shared.Trees.FileSystem;

namespace ProtonDrive.Sync.Engine.Shared.Trees.Propagation;

public class PropagationTreeNodeModel<TId> : AltIdentifiableFileSystemNodeModel<TId, TId>
    where TId : IEquatable<TId>
{
    public UpdateStatus RemoteStatus { get; set; }
    public UpdateStatus LocalStatus { get; set; }
    public bool Backup { get; set; }

    public override void CopyMetadataFrom(IFileSystemNodeModel<TId> other)
    {
        base.CopyMetadataFrom(other);

        if (other is PropagationTreeNodeModel<TId> item)
        {
            RemoteStatus = item.RemoteStatus;
            LocalStatus = item.LocalStatus;
            Backup = item.Backup;
        }
    }
}

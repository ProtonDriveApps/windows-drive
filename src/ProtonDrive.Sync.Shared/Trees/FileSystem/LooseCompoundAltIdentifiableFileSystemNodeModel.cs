using System;

namespace ProtonDrive.Sync.Shared.Trees.FileSystem;

public class LooseCompoundAltIdentifiableFileSystemNodeModel<TId, TAltId> : FileSystemNodeModel<TId>, ILooseCompoundAltIdentifiable<TId, TAltId>
    where TId : IEquatable<TId>
    where TAltId : IEquatable<TAltId>
{
    public LooseCompoundAltIdentity<TAltId> AltId { get; set; }

    public override void CopyMetadataFrom(IFileSystemNodeModel<TId> other)
    {
        base.CopyMetadataFrom(other);

        if (other is LooseCompoundAltIdentifiableFileSystemNodeModel<TId, TAltId> item)
        {
            AltId = item.AltId;
        }
    }
}

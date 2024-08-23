using System;

namespace ProtonDrive.Sync.Shared.Trees.FileSystem;

public class AltIdentifiableFileSystemNodeModel<TId, TAltId> : FileSystemNodeModel<TId>, IAltIdentifiable<TId, TAltId>
    where TId : IEquatable<TId>
    where TAltId : IEquatable<TAltId>
{
    private TAltId? _altId;

    public TAltId AltId
    {
        get => _altId ?? throw new ArgumentNullException(nameof(AltId));
        set => _altId = value;
    }

    public override void CopyMetadataFrom(IFileSystemNodeModel<TId> other)
    {
        base.CopyMetadataFrom(other);

        if (other is AltIdentifiableFileSystemNodeModel<TId, TAltId> item)
        {
            AltId = item.AltId;
        }
    }
}

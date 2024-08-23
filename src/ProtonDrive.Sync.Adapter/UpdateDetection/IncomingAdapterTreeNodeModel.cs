using System;
using ProtonDrive.Sync.Adapter.Trees.Adapter;
using ProtonDrive.Sync.Shared.Trees.FileSystem;

namespace ProtonDrive.Sync.Adapter.UpdateDetection;

public class IncomingAdapterTreeNodeModel<TId, TAltId> : AdapterTreeNodeModel<TId, TAltId>
    where TId : IEquatable<TId>
    where TAltId : IEquatable<TAltId>
{
    public long? SizeOnStorage { get; set; }

    public static IncomingAdapterTreeNodeModel<TId, TAltId> FromNodeModel(AdapterTreeNodeModel<TId, TAltId> other)
    {
        var model = new IncomingAdapterTreeNodeModel<TId, TAltId>();
        model.CopyFrom(other);

        return model;
    }

    public override void CopyMetadataFrom(IFileSystemNodeModel<TId> other)
    {
        base.CopyMetadataFrom(other);

        if (other is IncomingAdapterTreeNodeModel<TId, TAltId> item)
        {
            SizeOnStorage = item.SizeOnStorage;
        }
    }
}

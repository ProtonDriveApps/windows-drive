using System;
using ProtonDrive.DataAccess.Repositories;
using ProtonDrive.Sync.Adapter.Trees.Adapter;

namespace ProtonDrive.DataAccess.Databases;

internal sealed class FlatteningAdapterTreeNodeModelConverter<TId, TAltId> : IFlatteningConverter<AdapterTreeNodeModel<TId, TAltId>, FlattenedAdapterTreeNodeModel<TId, TAltId>>
where TId : IEquatable<TId>
where TAltId : IEquatable<TAltId>
{
    public AdapterTreeNodeModel<TId, TAltId> FromFlattened(FlattenedAdapterTreeNodeModel<TId, TAltId> flattened)
    {
        return new AdapterTreeNodeModel<TId, TAltId>
        {
            Id = flattened.Id,
            ParentId = flattened.ParentId,
            Type = flattened.Type,
            Name = flattened.Name,
            ContentVersion = flattened.ContentVersion,
            AltId = (flattened.VolumeId ?? default, flattened.AltId),
            RevisionId = flattened.RevisionId,
            Size = flattened.Size,
            LastWriteTime = flattened.LastWriteTime,
            Status = flattened.Status,
        };
    }

    public FlattenedAdapterTreeNodeModel<TId, TAltId> ToFlattened(AdapterTreeNodeModel<TId, TAltId> item)
    {
        return new FlattenedAdapterTreeNodeModel<TId, TAltId>
        {
            Id = item.Id,
            ParentId = item.ParentId,
            Type = item.Type,
            Name = item.Name,
            ContentVersion = item.ContentVersion,
            VolumeId = item.AltId.ItemId is null || item.AltId.ItemId.Equals(default) ? null : item.AltId.VolumeId,
            AltId = item.AltId.ItemId,
            RevisionId = item.RevisionId,
            Size = item.Size,
            LastWriteTime = item.LastWriteTime,
            Status = item.Status,
        };
    }
}

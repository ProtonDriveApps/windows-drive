using System;
using ProtonDrive.Sync.Shared;
using ProtonDrive.Sync.Shared.Trees.FileSystem;

namespace ProtonDrive.Sync.Engine.Shared.Trees;

internal static class AltIdentifiableFileSystemNodeModelExtensions
{
    public static TModel WithOwnId<TModel, TId>(this TModel model, TId value, Replica replica)
        where TModel : AltIdentifiableFileSystemNodeModel<TId, TId>
        where TId : IEquatable<TId>
    {
        return replica == Replica.Remote
            ? model.WithAltId(value)
            : model.WithId(value);
    }

    public static TModel WithOtherId<TModel, TId>(this TModel model, TId value, Replica replica)
        where TModel : AltIdentifiableFileSystemNodeModel<TId, TId>
        where TId : IEquatable<TId>
    {
        return replica == Replica.Local
            ? model.WithAltId(value)
            : model.WithId(value);
    }

    public static TId OwnId<TModel, TId>(this TModel model, Replica replica)
        where TModel : AltIdentifiableFileSystemNodeModel<TId, TId>
        where TId : IEquatable<TId>
    {
        return replica == Replica.Remote ? model.AltId : model.Id;
    }

    public static TId OtherId<TModel, TId>(this TModel model, Replica replica)
        where TModel : AltIdentifiableFileSystemNodeModel<TId, TId>
        where TId : IEquatable<TId>
    {
        return replica == Replica.Local ? model.AltId : model.Id;
    }
}

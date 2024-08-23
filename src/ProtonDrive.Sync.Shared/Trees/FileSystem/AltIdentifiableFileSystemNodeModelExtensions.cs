using System;

namespace ProtonDrive.Sync.Shared.Trees.FileSystem;

public static class AltIdentifiableFileSystemNodeModelExtensions
{
    public static TModel WithAltId<TModel, TId>(this TModel model, TId value)
        where TModel : AltIdentifiableFileSystemNodeModel<TId, TId>
        where TId : IEquatable<TId>
    {
        model.AltId = value;

        return model;
    }
}

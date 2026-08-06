namespace Proton.Drive.Sdk.Sync.Engine.Shared.Trees.Update;

public static class UpdateTreeNodeModelExtensions
{
    public static UpdateTreeNodeModel<TId> WithStatus<TId>(this UpdateTreeNodeModel<TId> model, UpdateStatus value)
        where TId : IEquatable<TId>
    {
        model.Status = value;

        return model;
    }
}

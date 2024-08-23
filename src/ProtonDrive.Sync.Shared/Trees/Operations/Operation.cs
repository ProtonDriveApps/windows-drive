namespace ProtonDrive.Sync.Shared.Trees.Operations;

public class Operation<TModel>
{
    public Operation(OperationType type, TModel model)
    {
        Type = type;
        Model = model;
    }

    public OperationType Type { get; }
    public TModel Model { get; set; }
}

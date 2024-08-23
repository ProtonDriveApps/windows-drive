namespace ProtonDrive.Sync.Shared.Adapters;

public sealed class ExecutionResult<TId> where TId : struct
{
    private ExecutionResult(ExecutionResultCode code, TId? conflictingNodeId = default)
    {
        Code = code;
        ConflictingNodeId = conflictingNodeId;
    }

    public ExecutionResultCode Code { get; }
    public TId? ConflictingNodeId { get; }

    public static ExecutionResult<TId> Success() => new(ExecutionResultCode.Success);

    public static ExecutionResult<TId> Failure(ExecutionResultCode code, TId? conflictingNodeId = default) =>
        new(code, conflictingNodeId);
}

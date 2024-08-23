namespace ProtonDrive.Sync.Shared.Adapters;

public static class ExecutionResultExtensions
{
    public static bool Succeeded<TId>(this ExecutionResult<TId> result) where TId : struct =>
        result.Code == ExecutionResultCode.Success;

    public static bool Failed<TId>(this ExecutionResult<TId> result) where TId : struct =>
        !Succeeded(result);
}

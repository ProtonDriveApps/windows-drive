namespace Proton.Drive.Sdk.Sync.Shared.ExecutionStatistics;

public interface IExecutionStatistics : IEquatable<IExecutionStatistics>
{
    int Succeeded { get; }
    int Failed { get; }
    int Skipped { get; }
    int Deleted { get; }

    public static IExecutionStatistics Zero { get; } = new ExecutionStatistics();

    public static IExecutionStatistics operator +(IExecutionStatistics x) => x;

    public static IExecutionStatistics operator +(IExecutionStatistics x, IExecutionStatistics y) => new ExecutionStatistics
    {
        Succeeded = x.Succeeded + y.Succeeded,
        Failed = x.Failed + y.Failed,
        Skipped = x.Skipped + y.Skipped,
        Deleted = x.Deleted + y.Deleted,
    };

    public static IExecutionStatistics operator -(IExecutionStatistics x) => new ExecutionStatistics
    {
        Succeeded = -x.Succeeded,
        Failed = -x.Failed,
        Skipped = -x.Skipped,
        Deleted = -x.Deleted,
    };

    public static IExecutionStatistics operator -(IExecutionStatistics x, IExecutionStatistics y) => new ExecutionStatistics
    {
        Succeeded = x.Succeeded - y.Succeeded,
        Failed = x.Failed - y.Failed,
        Skipped = x.Skipped - y.Skipped,
        Deleted = x.Deleted - y.Deleted,
    };

    bool IEquatable<IExecutionStatistics>.Equals(IExecutionStatistics? other)
    {
        return other != null &&
            Succeeded == other.Succeeded &&
            Failed == other.Failed &&
            Skipped == other.Skipped &&
            Deleted == other.Deleted;
    }
}

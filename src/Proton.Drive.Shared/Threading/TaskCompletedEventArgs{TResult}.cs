namespace Proton.Drive.Shared.Threading;

public class TaskCompletedEventArgs<TResult>
{
    public Task<TResult?> Task { get; }

    public TaskCompletedEventArgs(Task<TResult?> task)
    {
        Task = task;
    }
}

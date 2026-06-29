namespace Proton.Drive.Shared.Threading;

public class TaskCompletedEventArgs
{
    public Task Task { get; }

    public TaskCompletedEventArgs(Task task)
    {
        Task = task;
    }
}

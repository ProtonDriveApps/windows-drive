using System;
using System.Threading;
using System.Threading.Tasks;

namespace ProtonDrive.Shared.Threading;

public class SerialScheduler : IScheduler
{
    private volatile Task _currentTask = Task.CompletedTask;

    public Task<T> Schedule<T>(Func<Task<T>> function)
    {
        var taskCompletion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var newTask = taskCompletion.Task;

        while (true)
        {
            var expectedTask = _currentTask;
            var previousTask = Interlocked.CompareExchange(ref _currentTask, newTask, expectedTask);
            if (previousTask != expectedTask)
            {
                continue;
            }

            previousTask.ContinueWith(
                _ => taskCompletion.Wrap(function),
                TaskContinuationOptions.RunContinuationsAsynchronously);

            return newTask;
        }
    }

    public ISchedulerTimer CreateTimer()
    {
        throw new NotSupportedException();
    }
}

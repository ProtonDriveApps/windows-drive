using System;
using System.Threading;
using System.Threading.Tasks;

namespace ProtonDrive.Shared.Threading;

public class SingleFunction<TResult>
{
    private readonly Func<CancellationToken, Task<TResult?>> _function;

    private volatile Task<TResult?> _task = Task.FromResult(default(TResult));

    public SingleFunction(Func<TResult?> function)
        : this(_ => Task.FromResult(function()))
    { }

    public SingleFunction(Func<Task<TResult?>> function)
        : this(_ => function())
    { }

    public SingleFunction(Func<CancellationToken, Task<TResult?>> function)
    {
        _function = function;
    }

    public Task<TResult?> CurrentTask => _task;

    public virtual Task<TResult?> RunAsync(CancellationToken cancellationToken)
    {
        var initialCurrentTask = CurrentTask;
        if (!initialCurrentTask.IsCompleted)
        {
            return initialCurrentTask;
        }

        var taskCompletion = new TaskCompletionSource<TResult?>();
        var newTask = taskCompletion.Task;

        var currentTaskDuringComparison = Interlocked.CompareExchange(ref _task, newTask, initialCurrentTask);
        if (currentTaskDuringComparison != initialCurrentTask)
        {
            return currentTaskDuringComparison;
        }

        // Task.Run ensures the synchronous part of the user function doesn't block the caller.
        // This is important when the caller is on a UI thread or when called from SingleAction
        // which is often used in a fire and forget scenario.
        // The task created by Task.Run should not be cancelled for the TaskCompletionSource
        // to properly wrap the user function.
        Task.Run(() => taskCompletion.Wrap(() => _function(cancellationToken)), CancellationToken.None);

        return newTask;
    }
}

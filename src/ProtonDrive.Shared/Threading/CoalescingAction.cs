using System;
using System.Threading;
using System.Threading.Tasks;

namespace ProtonDrive.Shared.Threading;

/// <summary>
/// Implements consumer in a simple producer/consumer pattern. Multiple producers are producing work,
/// and a single consumer processes the work.
/// </summary>
public class CoalescingAction
{
    private readonly Func<CancellationToken, Task> _action;
    private readonly CancellationHandle _cancellationHandle;

    private volatile Task _currentTask = Task.CompletedTask;
    private volatile int _workRequested;

    public CoalescingAction(Action action)
        : this(_ =>
        {
            action();
            return Task.CompletedTask;
        })
    { }

    public CoalescingAction(IScheduler scheduler, Action action)
        : this(_ => scheduler.Schedule(action))
    { }

    public CoalescingAction(Func<Task> action)
        : this(_ => action())
    { }

    public CoalescingAction(Func<CancellationToken, Task> action)
    {
        _action = action;

        _cancellationHandle = new CancellationHandle();
    }

    public event EventHandler<TaskCompletedEventArgs>? Completed;

    public bool Running { get; private set; }

    public Task CurrentTask => _currentTask;

    public Task Run()
    {
        if (Interlocked.Exchange(ref _workRequested, 1) != 0)
        {
            return _currentTask;
        }

        Running = true;

        var taskCompletion = new TaskCompletionSource<Void>();
        var newTask = taskCompletion.Task;
        var cancellationToken = _cancellationHandle.Token;

        while (true)
        {
            var expectedTask = _currentTask;
            var previousTask = Interlocked.CompareExchange(ref _currentTask, newTask, expectedTask);
            if (previousTask != expectedTask)
            {
                continue;
            }

            var task = previousTask.IsCompleted
                ? Task.Run(() => Run(cancellationToken), cancellationToken)
                : previousTask.ContinueWith(_ => Run(cancellationToken), cancellationToken, TaskContinuationOptions.LazyCancellation, TaskScheduler.Current).Unwrap();

            task.ContinueWith(t => OnCompleted(t, taskCompletion), TaskContinuationOptions.ExecuteSynchronously);

            return newTask;
        }
    }

    public void Cancel()
    {
        _cancellationHandle.Cancel();
        _workRequested = 0;
    }

    private Task Run(CancellationToken cancellationToken)
    {
        Running = true;
        if (Interlocked.Exchange(ref _workRequested, 0) == 0)
        {
            return Task.CompletedTask;
        }

        return _action(cancellationToken);
    }

    private void OnCompleted(Task task, TaskCompletionSource<Void> taskCompletion)
    {
        Running = _workRequested != 0;
        Completed?.Invoke(this, new TaskCompletedEventArgs(task));
        taskCompletion.SetResult(default);
    }

    private struct Void { }
}

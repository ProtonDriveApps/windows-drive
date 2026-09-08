using System.Diagnostics;

namespace Proton.Drive.Shared.Threading;

public static class SchedulerExtensions
{
    public static Task Schedule(this IScheduler scheduler, Action action, CancellationToken cancellationToken = default)
    {
        return scheduler.Schedule(
            () =>
            {
                action.Invoke();
                return Task.CompletedTask;
            },
            cancellationToken);
    }

    public static Task<TResult> Schedule<TResult>(this IScheduler scheduler, Func<TResult> function, CancellationToken cancellationToken = default)
    {
        return scheduler.Schedule(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(function());
        });
    }

    [DebuggerHidden]
    [DebuggerStepThrough]
    public static Task Schedule(this IScheduler scheduler, Func<Task> action, CancellationToken cancellationToken = default)
    {
        return scheduler.Schedule<Void>(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            await action.Invoke().ConfigureAwait(false);
            return default;
        });
    }

    public static Task Schedule(this IScheduler scheduler, Func<CancellationToken, Task> action, CancellationToken cancellationToken = default)
    {
        return scheduler.Schedule<Void>(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            await action.Invoke(cancellationToken).ConfigureAwait(false);
            return default;
        });
    }

    public static Task<TResult> Schedule<TResult>(this IScheduler scheduler, Func<Task<TResult>> function, CancellationToken cancellationToken = default)
    {
        return scheduler.Schedule(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return function.Invoke();
        });
    }

    [DebuggerStepThrough]
    public static Task<IDisposable> LockAsync(this IScheduler scheduler, CancellationToken cancellationToken)
    {
        return AsyncLock.Acquire(scheduler, cancellationToken);
    }

    private struct Void;

    private class AsyncLock : IDisposable
    {
        // The TaskCreationOptions.RunContinuationsAsynchronously is needed to complete scheduled task as soon as the AsyncLock is disposed
        private readonly TaskCompletionSource<IDisposable> _acquisitionCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);

        // The TaskCreationOptions.RunContinuationsAsynchronously keeps the completion of the scheduled task off the thread that releases the lock
        private readonly TaskCompletionSource _releaseCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);

        private AsyncLock(IScheduler scheduler, CancellationToken cancellationToken)
        {
            var scheduledTask = scheduler.Schedule(Lock, cancellationToken);
            scheduledTask.ContinueWith(_ => _acquisitionCompletionSource.SetCanceled(), TaskContinuationOptions.OnlyOnCanceled);
            scheduledTask.ContinueWith(task => _acquisitionCompletionSource.SetException(task.Exception!.InnerException!), TaskContinuationOptions.OnlyOnFaulted);
        }

        private Task<IDisposable> Task => _acquisitionCompletionSource.Task;

        public static Task<IDisposable> Acquire(IScheduler scheduler, CancellationToken cancellationToken) => new AsyncLock(scheduler, cancellationToken).Task;

        public void Dispose() => _releaseCompletionSource.TrySetResult();

        private async Task Lock()
        {
            // The lack of TaskCreationOptions.RunContinuationsAsynchronously would block execution past this line until synchronous continuation of the task completes.
            _acquisitionCompletionSource.SetResult(this);

            await _releaseCompletionSource.Task.ConfigureAwait(false);
        }
    }
}

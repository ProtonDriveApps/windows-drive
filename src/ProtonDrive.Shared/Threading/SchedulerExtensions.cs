using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace ProtonDrive.Shared.Threading;

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
        cancellationToken.ThrowIfCancellationRequested();

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
        cancellationToken.ThrowIfCancellationRequested();

        return scheduler.Schedule<Void>(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            await action().ConfigureAwait(false);
            return default;
        });
    }

    public static Task<TResult> Schedule<TResult>(this IScheduler scheduler, Func<Task<TResult>> function, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return scheduler.Schedule(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return function.Invoke();
        });
    }

    public static Task<IDisposable> LockAsync(this IScheduler scheduler, CancellationToken cancellationToken)
    {
        return AsyncLock.Acquire(scheduler, cancellationToken);
    }

    private struct Void;

    private class AsyncLock : IDisposable
    {
        private readonly TaskCompletionSource<IDisposable> _acquireCompletionSource;
        private readonly TaskCompletionSource _releaseCompletionSource;

        private AsyncLock(IScheduler scheduler, CancellationToken cancellationToken)
        {
            _acquireCompletionSource = new TaskCompletionSource<IDisposable>();
            _releaseCompletionSource = new TaskCompletionSource();

            var scheduledTask = scheduler.Schedule(Lock, cancellationToken);
            scheduledTask.ContinueWith(_ => _acquireCompletionSource.SetCanceled(), TaskContinuationOptions.OnlyOnCanceled);
            scheduledTask.ContinueWith(task => _acquireCompletionSource.SetException(task.Exception!.InnerException!), TaskContinuationOptions.OnlyOnFaulted);
        }

        private Task<IDisposable> Task => _acquireCompletionSource.Task;

        public static Task<IDisposable> Acquire(IScheduler scheduler, CancellationToken cancellationToken) => new AsyncLock(scheduler, cancellationToken).Task;

        public void Dispose() => _releaseCompletionSource.TrySetResult();

        private async Task Lock()
        {
            _acquireCompletionSource.SetResult(this);
            await _releaseCompletionSource.Task.ConfigureAwait(false);
        }
    }
}

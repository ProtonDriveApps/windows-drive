using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using ProtonDrive.Shared.Threading;

namespace ProtonDrive.Sync.Shared;

public static class TransactedSchedulerExtensions
{
    [DebuggerHidden]
    [DebuggerStepThrough]
    public static Task ScheduleAndCommit(this ITransactedScheduler scheduler, Action action, CancellationToken cancellationToken = default)
    {
        return scheduler.Schedule(
            () =>
            {
                scheduler.ForceCommit = true;
                action.Invoke();
            },
            cancellationToken);
    }

    [DebuggerHidden]
    [DebuggerStepThrough]
    public static Task<TResult> ScheduleAndCommit<TResult>(this ITransactedScheduler scheduler, Func<TResult> function, CancellationToken cancellationToken = default)
    {
        return scheduler.Schedule(
            () =>
            {
                scheduler.ForceCommit = true;

                return function.Invoke();
            },
            cancellationToken);
    }

    [DebuggerHidden]
    [DebuggerStepThrough]
    public static Task ScheduleAndCommit(this ITransactedScheduler scheduler, Func<Task> action, CancellationToken cancellationToken = default)
    {
        return scheduler.Schedule(
            () =>
            {
                scheduler.ForceCommit = true;

                return action.Invoke();
            },
            cancellationToken);
    }

    [DebuggerHidden]
    [DebuggerStepThrough]
    public static Task<TResult> ScheduleAndCommit<TResult>(this ITransactedScheduler scheduler, Func<Task<TResult>> function, CancellationToken cancellationToken = default)
    {
        return scheduler.Schedule(
            () =>
            {
                scheduler.ForceCommit = true;

                return function.Invoke();
            },
            cancellationToken);
    }
}

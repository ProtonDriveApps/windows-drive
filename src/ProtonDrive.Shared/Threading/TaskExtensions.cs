using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace ProtonDrive.Shared.Threading;

public static class TaskExtensions
{
    [DebuggerHidden]
    [DebuggerStepThrough]
    public static Task WithCancellation(this Task task, CancellationToken cancellationToken)
    {
        if (task.IsCompleted)
        {
            return task;
        }

        return task.ContinueWith(
            completedTask => completedTask.GetAwaiter().GetResult(),
            cancellationToken,
            TaskContinuationOptions.NotOnCanceled | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    public static async Task TimeoutAfter(this Task task, TimeSpan timeout)
    {
        using var cancellationTokenSource = new CancellationTokenSource();

        var completedTask = await Task.WhenAny(task, Task.Delay(timeout, cancellationTokenSource.Token)).ConfigureAwait(false);
        if (completedTask != task)
        {
            throw new TimeoutException();
        }

        cancellationTokenSource.Cancel();

        // Task completed within timeout. The task may have faulted or been canceled.
        // Await the task so that any exceptions/cancellation is rethrown.
        await task.ConfigureAwait(false);
    }

    public static async Task<TResult> TimeoutAfter<TResult>(this Task<TResult> task, TimeSpan timeout)
    {
        using var cancellationTokenSource = new CancellationTokenSource();

        var completedTask = await Task.WhenAny(task, Task.Delay(timeout, cancellationTokenSource.Token)).ConfigureAwait(false);
        if (completedTask != task)
        {
            throw new TimeoutException();
        }

        cancellationTokenSource.Cancel();

        // Task completed within timeout. The task may have faulted or been canceled.
        // Await the task so that any exceptions/cancellation is rethrown.
        return await task.ConfigureAwait(false);
    }

    public static async Task WithTimeout(this Task task, Task timeoutTask)
    {
        if (await Task.WhenAny(task, timeoutTask).ConfigureAwait(false) != task)
        {
            throw new TimeoutException();
        }

        // Task completed within timeout. The task may have faulted or been canceled.
        // Await the task so that any exceptions/cancellation is rethrown.
        await task.ConfigureAwait(false);
    }

    public static async Task<TResult> WithTimeout<TResult>(this Task<TResult> task, Task timeoutTask)
    {
        if (await Task.WhenAny(task, timeoutTask).ConfigureAwait(false) != task)
        {
            throw new TimeoutException();
        }

        // Task completed within timeout. The task may have faulted or been canceled.
        // Await the task so that any exceptions/cancellation is rethrown.
        return await task.ConfigureAwait(false);
    }

    public static async Task TimeoutAfter(Func<CancellationToken, Task> action, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var timeoutSource = new CancellationTokenSource(timeout);
        using var linkedCancellationSource =
            CancellationTokenSource.CreateLinkedTokenSource(new[] { cancellationToken, timeoutSource.Token });

        try
        {
            await action(linkedCancellationSource.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && timeoutSource.IsCancellationRequested)
        {
            throw new TimeoutException();
        }
    }

    public static void Forget(this Task task)
    {
        // Do nothing
    }
}

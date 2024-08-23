using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace ProtonDrive.Client;

internal sealed class DataflowPipeline : IDataflowBlock, IDisposable, IAsyncDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly TaskCompletionSource _completionSource = new();
    private readonly List<IDataflowBlock> _blocks = [];

    private bool _isDisposed;

    public CancellationToken CancellationToken => _cancellationTokenSource.Token;

    /// <summary>
    /// A <see cref="Task"/> that represents the asynchronous operation and completion of the dataflow pipeline.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A dataflow pipeline is considered completed when it is not currently processing a message and when it has
    /// guaranteed that it will not process any more messages. The returned Task will transition to a completed
    /// state when all blocks have completed.
    /// </para>
    /// <para>
    /// If the dataflow pipeline fails, the completion <see cref="Task"/> contains first significant exception
    /// (not the <see cref="AggregateException"/>) that occurred in the pipeline. The remaining exceptions, if any,
    /// are considered "observed", so TaskScheduler.UnobservedTaskException is not raised.
    /// </para>
    /// </remarks>
    public Task Completion => GetCompletion();

    public DataflowPipeline Add(IDataflowBlock block)
    {
        block.Completion.ContinueWith(BlockFaultedOrCancelled, TaskContinuationOptions.NotOnRanToCompletion);

        _blocks.Add(block);

        return this;
    }

    public void Cancel() => _cancellationTokenSource.Cancel();

    public Task ThrowIfCompletedAsync()
    {
        var task = _completionSource.Task;

        if (!task.IsCompleted)
        {
            return Task.CompletedTask;
        }

        if (!task.IsCompletedSuccessfully)
        {
            return task;
        }

        throw new InvalidOperationException("Dataflow pipeline has completed");
    }

    void IDataflowBlock.Complete() => throw new NotSupportedException();

    void IDataflowBlock.Fault(Exception exception) => throw new NotSupportedException();

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        Cancel();

        Completion.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing).GetAwaiter().GetResult();

        _isDisposed = true;
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            return;
        }

        Cancel();

        await Completion.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);

        _isDisposed = true;
    }

    private async Task GetCompletion()
    {
        var blocksTask = Task.WhenAll(_blocks.Select(b => b.Completion));

        // After awaiting with the SuppressThrowing flag, the exception is considered "observed",
        // so TaskScheduler.UnobservedTaskException is not raised.
        await blocksTask.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);

        if (blocksTask.IsCompletedSuccessfully)
        {
            _completionSource.TrySetResult();
        }

        // If any block faulted or cancelled, the BlockFaultedOrCancelled method
        // transitions the task completion source to faulted or cancelled state.
        await _completionSource.Task.ConfigureAwait(false);
    }

    private void BlockFaultedOrCancelled(Task task)
    {
        if (task.IsCanceled)
        {
            _completionSource.TrySetCanceled();
        }
        else if (task.IsFaulted)
        {
            var innerException = task.Exception!.Flatten().InnerException!;

            // The first significant exception of the block that faulted first is set
            // as a pipeline completion exception. The remaining exceptions are ignored.
            _completionSource.TrySetException(innerException);
        }
        else
        {
            throw new ArgumentException($"The {nameof(task)} has not expected status {task.Status}", nameof(task));
        }

        Cancel();
    }
}

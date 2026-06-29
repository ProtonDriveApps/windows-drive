// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE-MIT file in the project root for full license information.
//
// Adapted from https://github.com/microsoft/BuildXL/blob/main/Public/Src/Utilities/Utilities/Tasks/DedicatedThreadTaskScheduler.cs

using System.Collections.Concurrent;

namespace Proton.Drive.Shared.Threading;

/// <summary>
/// Task scheduler for executing tasks on a dedicated thread
/// </summary>
public sealed class DedicatedThreadTaskScheduler : TaskScheduler, IDisposable
{
    /// <summary>
    /// Whether the current thread belongs to the task scheduler
    /// </summary>
    [ThreadStatic]
    private static bool _isDedicatedThread;

    private readonly ConcurrentQueue<Task> _tasks = new();
    private readonly ManualResetEventSlim _taskSignal = new();

    private bool _isDisposed;
    private int _pendingTaskCount;

    /// <summary>
    /// Creates a new task scheduler
    /// </summary>
    public DedicatedThreadTaskScheduler()
    {
        var thread = new Thread(RunDedicatedThread)
        {
            IsBackground = true,
        };

        thread.Start();
    }

    /// <inheritdoc />
    public override int MaximumConcurrencyLevel => 1;

    /// <inheritdoc />
    public void Dispose()
    {
        _isDisposed = true;
    }

    /// <inheritdoc />
    protected override void QueueTask(Task task)
    {
        Interlocked.Increment(ref _pendingTaskCount);
        _tasks.Enqueue(task);
        _taskSignal.Set();
    }

    /// <inheritdoc />
    protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
    {
        if (_isDedicatedThread)
        {
            return TryExecuteTask(task);
        }

        return false;
    }

    /// <inheritdoc />
    protected override IEnumerable<Task> GetScheduledTasks()
    {
        return _tasks;
    }

    private void RunDedicatedThread()
    {
        _isDedicatedThread = true;

        while (!_isDisposed)
        {
            _taskSignal.Reset();

            while (_tasks.TryDequeue(out var task))
            {
                Interlocked.Decrement(ref _pendingTaskCount);
                TryExecuteTask(task);

                if (_isDisposed)
                {
                    return;
                }
            }

            _taskSignal.Wait();
        }
    }
}

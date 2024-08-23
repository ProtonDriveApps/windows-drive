using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ProtonDrive.Shared.Threading;
using ProtonDrive.Sync.Shared;

namespace ProtonDrive.Sync.Adapter.UpdateDetection;

internal sealed class UpdateDetectionSequencer
{
    private readonly Queue<PostponedUpdateDetection> _postponedQueue = new();
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly IIdentitySource<long> _timeSequence = new ConcurrentIdentitySource();

    /// <summary>
    /// Raised when one of the requests to postpone update detection is removed.
    /// </summary>
    public event EventHandler? Resumed;

    /// <summary>
    /// Retrieves the next value from the always increasing number sequence.
    /// Safe to access concurrently from multiple threads.
    /// </summary>
    /// <returns>The next timestamp value.</returns>
    public long GetTimestamp() => _timeSequence.NextValue();

    /// <summary>
    /// Postpones processing of all evens received after the returned task completes.
    /// To resume event processing, dispose the returned disposable object.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A disposable object that should be disposed to resume update detection.</returns>
    public async Task<IDisposable> PostponeUpdateDetectionAsync(CancellationToken cancellationToken)
    {
        using var @lock = await _semaphore.LockAsync(cancellationToken).ConfigureAwait(false);

        var postponedUpdateDetection = new PostponedUpdateDetection(this, GetTimestamp());
        _postponedQueue.Enqueue(postponedUpdateDetection);

        return postponedUpdateDetection;
    }

    /// <summary>
    /// Checks whether processing of events received at the specified time is postponed.
    /// </summary>
    /// <param name="timestamp">The timestamp value retrieved when the event logs arrived.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>True if update detection is postponed; False otherwise.</returns>
    public async Task<bool> IsUpdateDetectionPostponedAsync(long timestamp, CancellationToken cancellationToken)
    {
        using var @lock = await _semaphore.LockAsync(cancellationToken).ConfigureAwait(false);

        PostponedUpdateDetection? postponedUpdateDetection;
        while (_postponedQueue.TryPeek(out postponedUpdateDetection) && postponedUpdateDetection.Timestamp == long.MaxValue)
        {
            _postponedQueue.Dequeue();
        }

        return timestamp > (postponedUpdateDetection?.Timestamp ?? long.MaxValue);
    }

    private void OnResumed()
    {
        Resumed?.Invoke(this, EventArgs.Empty);
    }

    private sealed class PostponedUpdateDetection : IDisposable
    {
        private readonly UpdateDetectionSequencer _sequencer;

        public PostponedUpdateDetection(UpdateDetectionSequencer sequencer, long timestamp)
        {
            _sequencer = sequencer;
            Timestamp = timestamp;
        }

        public long Timestamp { get; private set; }

        public void Dispose()
        {
            Timestamp = long.MaxValue;
            _sequencer.OnResumed();
        }
    }
}

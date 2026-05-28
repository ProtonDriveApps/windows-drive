namespace ProtonDrive.App.Instrumentation.Observability;

internal sealed class AttemptRetryMonitor<TId>
    where TId : notnull
{
    private readonly Lock _lock = new();
    private readonly Dictionary<TId, AttemptType> _statusByItemId = [];

    private int _firstTrySuccesses;
    private int _firstTryFailures;
    private int _retriedSuccesses;
    private int _retriedFailures;

    public void IncrementSuccess(TId id)
    {
        lock (_lock)
        {
            if (!_statusByItemId.Remove(id))
            {
                ++_firstTrySuccesses;
                return;
            }

            ++_retriedSuccesses;
        }
    }

    public void IncrementFailure(TId id)
    {
        lock (_lock)
        {
            if (!_statusByItemId.TryGetValue(id, out var itemStatus))
            {
                _statusByItemId.Add(id, AttemptType.FirstAttempt);
                ++_firstTryFailures;
                return;
            }

            switch (itemStatus)
            {
                case AttemptType.FirstAttempt:
                    _statusByItemId[id] = AttemptType.Retry;
                    ++_retriedFailures;
                    break;

                case AttemptType.Retry:
                    ++_retriedFailures;
                    break;
            }
        }
    }

    public AttemptRetryCounters GetAndResetCounters()
    {
        lock (_lock)
        {
            var counters = new AttemptRetryCounters(_firstTrySuccesses, _firstTryFailures, _retriedSuccesses, _retriedFailures);

            if (!counters.Any())
            {
                return counters;
            }

            _firstTrySuccesses = 0;
            _firstTryFailures = 0;
            _retriedSuccesses = 0;
            _retriedFailures = 0;

            return counters;
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _statusByItemId.Clear();
            _firstTrySuccesses = 0;
            _firstTryFailures = 0;
            _retriedSuccesses = 0;
            _retriedFailures = 0;
        }
    }
}

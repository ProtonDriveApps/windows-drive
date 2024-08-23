using System.Collections.Generic;

namespace ProtonDrive.Shared.Telemetry;

public abstract class UniqueItemCountersBase<T>
{
    private readonly HashSet<T> _successes = [];
    private readonly HashSet<T> _failures = [];
    private readonly object _lock = new();

    public void IncrementSuccesses(T key)
    {
        lock (_lock)
        {
            _successes.Add(key);
            _failures.Remove(key);
        }
    }

    public void IncrementFailures(T key)
    {
        lock (_lock)
        {
            _failures.Add(key);
            _successes.Remove(key);
        }
    }

    public (int Successes, int Failures) GetCounters()
    {
        lock (_lock)
        {
            return (_successes.Count, _failures.Count);
        }
    }

    public void Reset()
    {
        lock (_lock)
        {
            _successes.Clear();
            _failures.Clear();
        }
    }
}

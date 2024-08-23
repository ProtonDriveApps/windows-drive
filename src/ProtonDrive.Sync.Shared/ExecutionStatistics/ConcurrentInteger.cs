using System.Threading;

namespace ProtonDrive.Sync.Shared.ExecutionStatistics;

public class ConcurrentInteger
{
    private int _value;

    public int Value => _value;

    public void Set(int value) => Interlocked.Exchange(ref _value, value);

    public void Increment() => Interlocked.Increment(ref _value);

    public void Decrement() => Interlocked.Decrement(ref _value);

    public void Add(int value) => Interlocked.Add(ref value, value);
}

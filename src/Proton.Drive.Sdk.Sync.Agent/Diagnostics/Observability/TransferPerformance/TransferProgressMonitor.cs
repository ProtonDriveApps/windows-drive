using System.Collections.Concurrent;

namespace Proton.Drive.Sdk.Sync.Agent.Diagnostics.Observability.TransferPerformance;

internal sealed class TransferProgressMonitor<TKey>
    where TKey : IEquatable<TKey>
{
    private readonly ConcurrentDictionary<TKey, long> _transferProgress = [];
    private long _totalProgress;

    public long TotalProgress => _totalProgress;

    public void UpdateProgress(TKey key, long position)
    {
        _transferProgress.AddOrUpdate(key, position, UpdateValueFactory);

        return;

        long UpdateValueFactory(TKey id, long previousProgress)
        {
            var currentProgress = Math.Max(position, previousProgress);

            if (currentProgress > previousProgress)
            {
                Interlocked.Add(ref _totalProgress, currentProgress - previousProgress);
            }

            return currentProgress;
        }
    }

    public void Remove(TKey key)
    {
        _transferProgress.Remove(key, out _);
    }

    public void Clear()
    {
        _transferProgress.Clear();
        Interlocked.Exchange(ref _totalProgress, 0);
    }
}

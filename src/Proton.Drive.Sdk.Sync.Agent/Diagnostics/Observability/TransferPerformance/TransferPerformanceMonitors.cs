using System.Collections;
using System.Collections.Concurrent;
using Proton.Drive.Sdk.Sync.Shared.SyncActivity;
using Proton.Drive.Shared;
using Proton.Drive.Shared.Configuration;
using Proton.Drive.Shared.Extensions;

namespace Proton.Drive.Sdk.Sync.Agent.Diagnostics.Observability.TransferPerformance;

internal sealed class TransferPerformanceMonitors : IEnumerable<((SyncActivityType ActivityType, TransferContext Context, TransferPipeline Pipeline) Key, TransferPerformanceMonitor Monitor)>
{
    private readonly AppConfig _appConfig;
    private readonly IClock _clock;

    private readonly ConcurrentDictionary<(SyncActivityType, TransferContext, TransferPipeline), TransferPerformanceMonitor> _monitors = new();

    public TransferPerformanceMonitors(AppConfig appConfig, IClock clock)
    {
        _appConfig = appConfig;
        _clock = clock;
    }

    public TransferPerformanceMonitor this[(SyncActivityType ActivityType, TransferContext Context, TransferPipeline Pipeline) key]
    {
        get => _monitors.GetOrAdd(key, _ => new TransferPerformanceMonitor(_appConfig, _clock));
    }

    public IEnumerator<((SyncActivityType ActivityType, TransferContext Context, TransferPipeline Pipeline) Key, TransferPerformanceMonitor Monitor)> GetEnumerator()
    {
        return _monitors.Select(keyValuePair => (keyValuePair.Key, keyValuePair.Value)).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public void Clear()
    {
        _monitors.Clear();
    }
}
